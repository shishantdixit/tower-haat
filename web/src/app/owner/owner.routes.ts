import { Routes } from '@angular/router';

export default [{ path: '', loadComponent: () => import('./owner-home') }] satisfies Routes;
