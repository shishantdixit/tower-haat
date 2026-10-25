import { Routes } from '@angular/router';

export default [
  { path: '', loadComponent: () => import('./admin-home') },
  { path: 'businesses', loadComponent: () => import('./approvals') },
  { path: 'types', loadComponent: () => import('./business-types') },
  { path: 'stats', loadComponent: () => import('./stats') },
  { path: 'backups', loadComponent: () => import('./backups') },
  { path: 'errors', loadComponent: () => import('./error-log') },
  { path: 'societies', loadComponent: () => import('./societies') },
  { path: 'societies/:societyId', loadComponent: () => import('./society-detail') },
] satisfies Routes;
