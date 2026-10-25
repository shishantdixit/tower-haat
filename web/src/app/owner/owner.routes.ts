import { Routes } from '@angular/router';

export default [
  { path: '', loadComponent: () => import('./owner-home') },
  { path: 'register', loadComponent: () => import('./business-form'), data: { mode: 'register' } },
  { path: 'profile', loadComponent: () => import('./business-form'), data: { mode: 'edit' } },
  { path: 'settings', loadComponent: () => import('./settings-form') },
  { path: 'photos', loadComponent: () => import('./photos') },
  { path: 'menu', loadComponent: () => import('./menu') },
  { path: 'menu/new', loadComponent: () => import('./item-form') },
  { path: 'menu/:itemId', loadComponent: () => import('./item-form') },
] satisfies Routes;
