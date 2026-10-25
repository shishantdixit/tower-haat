import { Routes } from '@angular/router';

export default [
  { path: '', loadComponent: () => import('./customer-home') },
  { path: 'b/:businessId', loadComponent: () => import('./business-page') },
] satisfies Routes;
