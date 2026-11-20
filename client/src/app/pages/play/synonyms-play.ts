import { Component, inject, input } from '@angular/core';
import { PlayerState } from '../../core/models';
import { QuizHubService } from '../../core/quiz-hub.service';
import { Countdown } from '../../shared/countdown';

/** Écran du téléphone pendant les duos et l'épreuve des Synonymes. */
@Component({
  selector: 'app-synonyms-play',
  imports: [Countdown],
  template: `
    @let s = state();
    @let turn = s.turn;
    @let others = partners();

    @switch (s.phase) {
      @case ('Duos') {
        <section class="message">
          <p class="label">Ton équipe</p>
          @if (others.length) {
            <h2 class="partner">{{ others.join(' et ') }}</h2>
            <p>{{ others.length > 1 ? 'Ce sont tes partenaires' : 'C’est ton partenaire' }} pour les Synonymes !</p>
          } @else {
            <p>Tu regardes cette épreuve depuis les tribunes.</p>
          }
        </section>
      }

      @case ('SynonymsIdle') {
        <section class="message">
          <div class="emoji">🗣️</div>
          <h2>Synonymes</h2>
          @if (s.myTeam; as team) {
            <p>Avec {{ others.join(' et ') }}</p>
            <p class="stat">{{ team.found }} mot{{ team.found > 1 ? 's' : '' }} trouvé{{ team.found > 1 ? 's' : '' }}</p>
          }
          <p>Regarde l’écran : l’animateur choisit l’équipe qui joue.</p>
        </section>
      }

      @default {
        @if (turn) {
          @if (s.phase === 'SynonymsTurnEnd') {
            <section class="message">
              <div class="emoji">⏱️</div>
              <h2>Temps écoulé !</h2>
              <p>{{ turn.giverPseudo }} → {{ turn.guessers.join(' et ') }}</p>
              <p class="stat">{{ turn.found }} mot{{ turn.found > 1 ? 's' : '' }} trouvé{{ turn.found > 1 ? 's' : '' }}</p>
            </section>
          } @else {
            @switch (s.role) {
              @case ('giver') {
                @if (s.phase === 'SynonymsReady') {
                  <section class="message">
                    <div class="emoji">🎤</div>
                    <h2>C’est toi qui fais deviner !</h2>
                    <p>À {{ turn.guessers.join(' et ') }}, uniquement avec des synonymes.</p>
                    <p class="hint">Le mot va s’afficher ici. Cache bien ton écran !</p>
                  </section>
                } @else {
                  <section class="giver">
                    <app-countdown [timer]="s.timer" />
                    @for (w of [s.word]; track w) {
                      <div class="word">{{ w }}</div>
                    }
                    <p class="stat">{{ turn.found }} trouvé{{ turn.found > 1 ? 's' : '' }}</p>
                    <div class="actions">
                      <button type="button" class="act found" (click)="word(true)">Trouvé ✓</button>
                      <button type="button" class="act pass" (click)="word(false)">Passer</button>
                    </div>
                  </section>
                }
              }
              @case ('guesser') {
                <section class="message">
                  <div class="emoji">👂</div>
                  <h2>À toi de deviner !</h2>
                  <p>{{ turn.giverPseudo }} te fait deviner des mots.</p>
                  @if (s.phase === 'SynonymsRunning') {
                    <app-countdown [timer]="s.timer" />
                    <p class="stat">{{ turn.found }} trouvé{{ turn.found > 1 ? 's' : '' }}</p>
                  }
                </section>
              }
              @default {
                <section class="message">
                  <div class="emoji">👀</div>
                  <h2>Au tour de {{ turn.giverPseudo }}</h2>
                  <p>qui fait deviner à {{ turn.guessers.join(' et ') }}</p>
                  @if (s.phase === 'SynonymsRunning') {
                    <app-countdown [timer]="s.timer" />
                    <p class="stat">{{ turn.found }} trouvé{{ turn.found > 1 ? 's' : '' }}</p>
                  }
                </section>
              }
            }
          }
        }
      }
    }
  `,
  styleUrl: './synonyms-play.scss',
})
export class SynonymsPlay {
  private readonly hub = inject(QuizHubService);
  readonly state = input.required<PlayerState>();

  protected partners(): string[] {
    const s = this.state();
    return (s.myTeam?.members ?? []).filter((m) => m.id !== s.playerId).map((m) => m.pseudo);
  }

  protected word(found: boolean): void {
    const seq = this.state().turn?.wordSeq;
    if (seq === undefined) return;
    navigator.vibrate?.(found ? 40 : 15);
    void this.hub.call('GiverWord', seq, found);
  }
}
