import { Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { TimerDto } from '../core/models';

/**
 * Chrono affiché en local à partir du temps restant envoyé par le serveur
 * (pas de dépendance à l'heure du téléphone, qui peut être décalée).
 */
@Component({
  selector: 'app-countdown',
  template: `
    @if (timer()) {
      <div class="countdown" [class.urgent]="seconds() <= 5" [class.big]="size() === 'big'">
        <div class="bar"><div class="fill" [style.width.%]="percent()"></div></div>
        <span class="secs">{{ seconds() }}</span>
      </div>
    }
  `,
  styles: `
    :host { display: block; width: 100%; }
    .countdown { display: flex; align-items: center; gap: 0.75rem; width: 100%; }
    .bar { flex: 1; height: 0.9rem; border-radius: 999px; background: rgb(255 255 255 / 0.15); overflow: hidden; }
    .fill { height: 100%; border-radius: inherit; background: var(--qp-yellow); transition: width 0.25s linear; }
    .secs { min-width: 2.5ch; text-align: right; font-size: 1.6rem; font-weight: 900; font-variant-numeric: tabular-nums; }
    .big .bar { height: 1.4rem; }
    .big .secs { font-size: 3rem; }
    .urgent .fill { background: var(--qp-red); }
    .urgent .secs { color: var(--qp-red); animation: pulse 0.5s ease-in-out infinite alternate; }
    @keyframes pulse { to { transform: scale(1.15); } }
  `,
})
export class Countdown {
  readonly timer = input<TimerDto | null>(null);
  readonly size = input<'normal' | 'big'>('normal');

  private readonly now = signal(performance.now());
  private endsAt = 0;

  readonly remainingMs = computed(() => {
    this.now();
    return Math.max(0, this.endsAt - performance.now());
  });
  readonly seconds = computed(() => Math.ceil(this.remainingMs() / 1000));
  readonly percent = computed(() => {
    const t = this.timer();
    return t && t.durationMs > 0 ? (this.remainingMs() / t.durationMs) * 100 : 0;
  });

  constructor() {
    effect(() => {
      const t = this.timer();
      this.endsAt = t ? performance.now() + t.remainingMs : 0;
      this.now.set(performance.now());
    });
    const handle = setInterval(() => this.now.set(performance.now()), 200);
    inject(DestroyRef).onDestroy(() => clearInterval(handle));
  }
}
