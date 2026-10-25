import { Routes } from '@angular/router';

export default [{ path: '', loadComponent: () => import('./admin-home') }] satisfies Routes;
