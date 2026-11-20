import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  template: `
    <main>
      <h1 class="qp-logo">Quiz Party</h1>
      <p>Le quiz entre amis, dans la même pièce.</p>
      <div class="actions">
        <a class="qp-btn pink" routerLink="/play">Je joue</a>
        <a class="qp-btn ghost" routerLink="/host">Écran animateur</a>
      </div>
    </main>
  `,
  styles: `
    main {
      min-height: 100dvh; display: grid; place-content: center; justify-items: center; gap: 1.5rem;
      padding: 2rem; text-align: center;
    }
    p { margin: 0; font-size: 1.2rem; color: var(--qp-muted); }
    .actions { display: flex; flex-wrap: wrap; gap: 1rem; justify-content: center; }
    a { text-decoration: none; }
  `,
})
export class Home {}
