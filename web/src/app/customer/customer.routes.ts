import { Routes } from '@angular/router';

export default [
  { path: '', loadComponent: () => import('./customer-home') },
  { path: 'b/:businessId', loadComponent: () => import('./business-page') },
  { path: 'checkout', loadComponent: () => import('./checkout') },
  { path: 'orders', loadComponent: () => import('./my-orders') },
  { path: 'orders/:orderId', loadComponent: () => import('./order-detail') },
] satisfies Routes;
