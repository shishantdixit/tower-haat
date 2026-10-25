import { Routes } from '@angular/router';

export default [{ path: '', loadComponent: () => import('./customer-home') }] satisfies Routes;
