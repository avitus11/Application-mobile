import { Component, input } from '@angular/core';
import { RankingEntry } from '../core/models';

@Component({
  selector: 'app-leaderboard',
  template: `
    <ol class="board" [class.compact]="compact()">
      @for (entry of ranking(); track entry.playerId; let i = $index) {
        <li
          [class.me]="entry.playerId === highlightId()"
          [class.podium]="entry.rank <= 3"
          [style.animation-delay.ms]="i * 60"
        >
          <span class="rank">{{ entry.rank }}</span>
          <span class="pseudo">{{ entry.pseudo }}</span>
          <span class="score">{{ entry.score }} pt{{ entry.score > 1 ? 's' : '' }}</span>
        </li>
      } @empty {
        <li class="empty">Aucun joueur</li>
      }
    </ol>
  `,
  styles: `
    .board { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.5rem; }
    li {
      display: flex; align-items: center; gap: 1rem; padding: 0.7rem 1.1rem; border-radius: 1rem;
      background: rgb(255 255 255 / 0.1); font-size: 1.5rem; font-weight: 800;
      animation: slide-in 0.4s ease-out both;
    }
    li.podium { background: rgb(255 255 255 / 0.2); }
    li.podium:first-child { background: var(--qp-yellow); color: var(--qp-ink); }
    li.me { outline: 3px solid var(--qp-pink); }
    .rank { width: 2ch; text-align: center; font-size: 1.8rem; }
    .pseudo { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .score { font-variant-numeric: tabular-nums; }
    .compact li { font-size: 1.1rem; padding: 0.5rem 0.9rem; }
    .compact .rank { font-size: 1.2rem; }
    .empty { justify-content: center; opacity: 0.7; }
    @keyframes slide-in { from { opacity: 0; transform: translateX(-2rem); } }
  `,
})
export class Leaderboard {
  readonly ranking = input.required<RankingEntry[]>();
  readonly highlightId = input<string | null>(null);
  readonly compact = input(false);
}
