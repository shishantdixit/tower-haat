import { Routes } from '@angular/router';

export default [
  { path: '', loadComponent: () => import('./admin-home') },
  { path: 'businesses', loadComponent: () => import('./approvals') },
  { path: 'types', loadComponent: () => import('./business-types') },
  { path: 'societies', loadComponent: () => import('./societies') },
  { path: 'societies/:societyId', loadComponent: () => import('./society-detail') },
] satisfies Routes;
