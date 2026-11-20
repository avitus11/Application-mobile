import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import QRCode from 'qrcode';
import { QuizHubService } from '../../core/quiz-hub.service';
import { LETTERS } from '../../core/models';
import { Countdown } from '../../shared/countdown';
import { Leaderboard } from '../../shared/leaderboard';

@Component({
  selector: 'app-host',
  imports: [Countdown, Leaderboard],
  templateUrl: './host.html',
  styleUrl: './host.scss',
})
export class Host implements OnInit {
  protected readonly hub = inject(QuizHubService);
  protected readonly letters = LETTERS;

  protected readonly state = this.hub.hostState;
  protected readonly phase = computed(() => this.state()?.phase ?? 'Lobby');
  protected readonly question = computed(() => this.state()?.question ?? null);
  protected readonly players = computed(() => this.state()?.players ?? []);
  protected readonly connectedCount = computed(() => this.players().filter((p) => p.connected).length);
  protected readonly qrDataUrl = signal<string | null>(null);
  protected readonly confirmNewGame = signal(false);

  constructor() {
    effect(() => {
      const url = this.state()?.joinUrl;
      if (!url) {
        this.qrDataUrl.set(null);
        return;
      }
      QRCode.toDataURL(url, { margin: 1, width: 480, color: { dark: '#1b0b33', light: '#ffffff' } })
        .then((data) => this.qrDataUrl.set(data))
        .catch(() => this.qrDataUrl.set(null));
    });
  }

  ngOnInit(): void {
    void this.hub.connectAsHost();
  }

  protected createGame(): void {
    this.confirmNewGame.set(false);
    void this.hub.call('CreateGame');
  }

  /** « Nouvelle partie » en cours de jeu : un deuxième clic confirme (évite les fausses manips). */
  protected askNewGame(): void {
    if (this.confirmNewGame()) {
      this.createGame();
      return;
    }
    this.confirmNewGame.set(true);
    setTimeout(() => this.confirmNewGame.set(false), 4000);
  }

  protected next(): void {
    void this.hub.call('NextQuestion');
  }

  protected startTimer(): void {
    void this.hub.call('StartTimer');
  }

  protected lock(): void {
    void this.hub.call('LockAnswers');
  }

  protected reveal(): void {
    void this.hub.call('RevealAnswer');
  }

  protected toggleScores(): void {
    void this.hub.call('SetScoresVisible', !this.state()?.scoresVisible);
  }

  protected removePlayer(id: string): void {
    void this.hub.call('RemovePlayer', id);
  }

  protected percent(count: number): number {
    const total = this.state()?.answersCount ?? 0;
    return total > 0 ? Math.round((count / total) * 100) : 0;
  }
}
