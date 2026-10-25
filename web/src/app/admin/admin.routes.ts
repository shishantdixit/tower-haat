import { Routes } from '@angular/router';

export default [
  { path: '', loadComponent: () => import('./societies') },
  { path: 'societies/:societyId', loadComponent: () => import('./society-detail') },
] satisfies Routes;
