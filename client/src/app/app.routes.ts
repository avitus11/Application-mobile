import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/home/home').then((m) => m.Home) },
  { path: 'host', loadComponent: () => import('./pages/host/host').then((m) => m.Host), title: 'Quiz Party · Animateur' },
  { path: 'play', loadComponent: () => import('./pages/play/play').then((m) => m.Play), title: 'Quiz Party' },
  { path: '**', redirectTo: '' },
];
