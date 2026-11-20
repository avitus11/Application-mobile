import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { QuizHubService } from '../../core/quiz-hub.service';
import { LETTERS } from '../../core/models';
import { Countdown } from '../../shared/countdown';
import { Leaderboard } from '../../shared/leaderboard';
import { SynonymsPlay } from './synonyms-play';

@Component({
  selector: 'app-play',
  imports: [FormsModule, Countdown, Leaderboard, SynonymsPlay],
  templateUrl: './play.html',
  styleUrl: './play.scss',
})
export class Play implements OnInit {
  protected readonly hub = inject(QuizHubService);
  private readonly route = inject(ActivatedRoute);
  protected readonly letters = LETTERS;

  protected readonly state = this.hub.playerState;
  protected readonly phase = computed(() => this.state()?.phase ?? 'Lobby');
  protected readonly joined = computed(() => this.state()?.joined ?? false);
  protected readonly isSynonymsStage = computed(() => {
    const p = this.phase();
    return p === 'Duos' || p.startsWith('Synonyms');
  });
  protected readonly result = computed<'correct' | 'wrong' | 'none' | null>(() => {
    const s = this.state();
    if (!s || s.phase !== 'Reveal' || s.correctAnswer === null) return null;
    if (s.myAnswer === null) return 'none';
    return s.myAnswer === s.correctAnswer ? 'correct' : 'wrong';
  });

  protected code = '';
  protected pseudo = '';
  protected readonly joining = signal(false);
  protected readonly joinError = signal<string | null>(null);

  ngOnInit(): void {
    this.code = (this.route.snapshot.queryParamMap.get('code') ?? '').toUpperCase();
    void this.hub.connectAsPlayer();
  }

  protected async join(): Promise<void> {
    if (this.joining()) return;
    this.joining.set(true);
    this.joinError.set(null);
    try {
      const result = await this.hub.join(this.code, this.pseudo);
      if (!result.ok) this.joinError.set(result.error ?? 'Impossible de rejoindre.');
    } catch {
      this.joinError.set('Le serveur ne répond pas. Vérifie que tu es sur le Wi-Fi de la maison.');
    } finally {
      this.joining.set(false);
    }
  }

  protected choose(index: number): void {
    if (this.phase() !== 'Question') return;
    navigator.vibrate?.(30);
    void this.hub.answer(index);
  }
}
