import { Component, inject, input, signal } from '@angular/core';
import { HostState } from '../../core/models';
import { QuizHubService } from '../../core/quiz-hub.service';
import { Countdown } from '../../shared/countdown';

/** Partie centrale de l'écran animateur pendant la révélation des duos et l'épreuve des Synonymes. */
@Component({
  selector: 'app-synonyms-host',
  imports: [Countdown],
  template: `
    @let s = state();
    @switch (s.phase) {
      @case ('Duos') {
        <section class="stage">
          <h1 class="qp-logo title">Les duos !</h1>
          <p class="muted">1er avec le dernier, 2e avec l'avant-dernier…</p>
          <div class="teams">
            @for (t of s.teams; track t.number; let i = $index) {
              <div class="team team-{{ i % 6 }} reveal" [style.animation-delay.ms]="300 + i * 450">
                <span class="num">Équipe {{ t.number }}</span>
                <span class="names">
                  @for (m of t.members; track m.id; let last = $last) {
                    {{ m.pseudo }}@if (!last) { <span class="amp">&amp;</span> }
                  }
                </span>
              </div>
            }
          </div>
        </section>
      }

      @case ('SynonymsIdle') {
        <section class="stage">
          <h1 class="qp-logo title">Synonymes</h1>
          @if (s.allTurnsDone) {
            <p class="done-banner">Toutes les équipes ont joué !</p>
          } @else {
            <p class="muted">Choisis l'équipe qui joue</p>
          }
          <div class="teams">
            @for (t of s.teams; track t.number; let i = $index) {
              <button
                type="button"
                class="team team-{{ i % 6 }} pick"
                [disabled]="t.turnsDone >= t.turnsTotal"
                (click)="select(t.number)"
              >
                <span class="num">Équipe {{ t.number }}</span>
                <span class="names">
                  @for (m of t.members; track m.id; let last = $last) {
                    {{ m.pseudo }}@if (!last) { <span class="amp">&amp;</span> }
                  }
                </span>
                <span class="meta">
                  <span>{{ t.found }} mot{{ t.found > 1 ? 's' : '' }}</span>
                  <span class="dots">
                    @for (d of dots(t.turnsTotal); track $index) {
                      <i [class.on]="$index < t.turnsDone"></i>
                    }
                  </span>
                </span>
              </button>
            }
          </div>
          <p class="small muted">{{ s.wordsLeft }} mots restants dans la liste</p>
        </section>
      }

      @default {
        @if (s.turn; as turn) {
          <section class="stage turn">
            <p class="who">
              <strong>{{ turn.giverPseudo }}</strong>
              fait deviner à
              <strong>{{ turn.guessers.join(' et ') }}</strong>
            </p>

            @if (s.phase === 'SynonymsReady') {
              <p class="ready">Prêts ?</p>
              <p class="muted">Le mot va s'afficher sur le téléphone de {{ turn.giverPseudo }}.</p>
            }

            @if (s.phase === 'SynonymsRunning') {
              <app-countdown [timer]="s.timer" size="big" />
              <div class="counter">
                <span class="big-num">{{ turn.found }}</span>
                <span>mot{{ turn.found > 1 ? 's' : '' }} trouvé{{ turn.found > 1 ? 's' : '' }}</span>
              </div>
              <button
                type="button"
                class="peek"
                (pointerdown)="peek.set(true)"
                (pointerup)="peek.set(false)"
                (pointerleave)="peek.set(false)"
                (pointercancel)="peek.set(false)"
              >
                {{ peek() ? s.word : 'Maintenir pour voir le mot' }}
              </button>
            }

            @if (s.phase === 'SynonymsTurnEnd') {
              <div class="result">
                <span class="big-num">{{ turn.found }}</span>
                <span>mot{{ turn.found > 1 ? 's' : '' }} trouvé{{ turn.found > 1 ? 's' : '' }} !</span>
              </div>
              @if (turn.passed > 0) {
                <p class="muted">{{ turn.passed }} passé{{ turn.passed > 1 ? 's' : '' }}</p>
              }
            }
          </section>
        }
      }
    }
  `,
  styleUrl: './synonyms-host.scss',
})
export class SynonymsHost {
  private readonly hub = inject(QuizHubService);
  readonly state = input.required<HostState>();
  protected readonly peek = signal(false);

  protected dots(n: number): unknown[] {
    return Array.from({ length: n });
  }

  protected select(teamNumber: number): void {
    void this.hub.call('SelectTurn', teamNumber);
  }
}
