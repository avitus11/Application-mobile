import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { HostState, JoinResult, PlayerState } from './models';

export type ConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

/** Clé du stockage local : seul l'identifiant joueur est gardé, pour se reconnecter (pas de cookie). */
const PLAYER_ID_KEY = 'quizparty.playerId';

/**
 * Connexion SignalR unique, partagée par l'écran animateur et l'écran joueur.
 * L'état du jeu arrive du serveur et est exposé sous forme de signals.
 */
@Injectable({ providedIn: 'root' })
export class QuizHubService {
  readonly status = signal<ConnectionStatus>('disconnected');
  readonly hostState = signal<HostState | null>(null);
  readonly playerState = signal<PlayerState | null>(null);
  readonly lastError = signal<string | null>(null);

  private connection: HubConnection | null = null;
  private starting: Promise<void> | null = null;
  private role: 'host' | 'player' | null = null;

  async connectAsHost(): Promise<void> {
    this.role = 'host';
    await this.ensureStarted();
    try {
      this.hostState.set(await this.connection!.invoke<HostState>('HostConnect'));
      this.lastError.set(null);
    } catch (e) {
      this.lastError.set(errorMessage(e));
    }
  }

  async connectAsPlayer(): Promise<void> {
    this.role = 'player';
    await this.ensureStarted();
    await this.restorePlayer();
  }

  async join(code: string, pseudo: string): Promise<JoinResult> {
    await this.ensureStarted();
    const result = await this.connection!.invoke<JoinResult>('Join', code.trim().toUpperCase(), pseudo);
    if (result.ok && result.playerId) {
      safeStorage.set(PLAYER_ID_KEY, result.playerId);
      this.playerState.set(await this.connection!.invoke<PlayerState>('GetPlayerState'));
    }
    return result;
  }

  answer(choice: number): Promise<void> {
    return this.call('Answer', choice);
  }

  /** Commandes de l'animateur : les erreurs sont affichées dans lastError. */
  call(method: string, ...args: unknown[]): Promise<void> {
    if (!this.connection) return Promise.resolve();
    return this.connection
      .invoke(method, ...args)
      .then(() => this.lastError.set(null))
      .catch((e) => this.lastError.set(errorMessage(e)));
  }

  private async restorePlayer(): Promise<void> {
    const id = safeStorage.get(PLAYER_ID_KEY);
    if (id) {
      const ok = await this.connection!.invoke<boolean>('Rejoin', id);
      // Nouvelle partie ou joueur retiré : on oublie l'ancien identifiant.
      if (!ok) safeStorage.remove(PLAYER_ID_KEY);
    }
    this.playerState.set(await this.connection!.invoke<PlayerState>('GetPlayerState'));
  }

  private ensureStarted(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) return Promise.resolve();
    if (this.starting) return this.starting;

    if (!this.connection) {
      this.connection = new HubConnectionBuilder()
        .withUrl('/hub')
        // Réessaie indéfiniment : un téléphone qui se met en veille finit par revenir.
        .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (ctx) => Math.min(1000 * (ctx.previousRetryCount + 1), 5000) })
        .configureLogging(LogLevel.Warning)
        .build();

      this.connection.on('hostState', (s: HostState) => {
        this.hostState.set(s);
      });
      this.connection.on('playerState', (s: PlayerState) => {
        this.playerState.set(s);
      });
      this.connection.onreconnecting(() => this.status.set('reconnecting'));
      this.connection.onreconnected(() => {
        this.status.set('connected');
        void this.afterReconnect();
      });
      this.connection.onclose(() => {
        this.status.set('disconnected');
        // Fermeture définitive (veille longue) : on relance une connexion.
        setTimeout(() => void this.reconnectFromScratch(), 2000);
      });
      document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible' && this.connection?.state === HubConnectionState.Disconnected) {
          void this.reconnectFromScratch();
        }
      });
    }

    this.status.set('connecting');
    this.starting = this.connection
      .start()
      .then(() => this.status.set('connected'))
      .catch(async (e) => {
        this.status.set('disconnected');
        this.lastError.set('Connexion au serveur impossible. Nouvel essai…');
        await delay(2000);
        this.starting = null;
        return this.ensureStarted();
      })
      .finally(() => (this.starting = null));
    return this.starting;
  }

  private async reconnectFromScratch(): Promise<void> {
    if (this.connection?.state !== HubConnectionState.Disconnected) return;
    await this.ensureStarted();
    await this.afterReconnect();
  }

  private async afterReconnect(): Promise<void> {
    if (this.role === 'host') await this.connectAsHost();
    if (this.role === 'player') await this.restorePlayer();
  }
}

function errorMessage(e: unknown): string {
  const raw = e instanceof Error ? e.message : String(e);
  // SignalR préfixe les HubException : on ne garde que le message du serveur.
  const marker = 'HubException: ';
  const i = raw.indexOf(marker);
  return i >= 0 ? raw.slice(i + marker.length) : raw;
}

const delay = (ms: number) => new Promise((r) => setTimeout(r, ms));

/** localStorage peut être indisponible (navigation privée stricte) : on ne plante pas pour autant. */
const safeStorage = {
  get(key: string): string | null {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  },
  set(key: string, value: string): void {
    try {
      localStorage.setItem(key, value);
    } catch {
      /* ignoré */
    }
  },
  remove(key: string): void {
    try {
      localStorage.removeItem(key);
    } catch {
      /* ignoré */
    }
  },
};
