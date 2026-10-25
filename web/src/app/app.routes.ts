import { Routes } from '@angular/router';
import {
  platformAdminGuard,
  profileGuard,
  signedInGuard,
  signedOutGuard,
} from './core/auth/guards';

// One lazy chunk per area.
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'customer' },
  { path: 'login', canActivate: [signedOutGuard], loadComponent: () => import('./account/login') },
  {
    path: 'signup',
    canActivate: [signedInGuard],
    loadComponent: () => import('./account/profile-form'),
    data: { mode: 'signup' },
  },
  {
    path: 'profile',
    canActivate: [signedInGuard, profileGuard],
    loadComponent: () => import('./account/profile-form'),
    data: { mode: 'edit' },
  },
  {
    path: 'customer',
    canActivate: [signedInGuard, profileGuard],
    loadChildren: () => import('./customer/customer.routes'),
  },
  {
    path: 'owner',
    canActivate: [signedInGuard, profileGuard],
    loadChildren: () => import('./owner/owner.routes'),
  },
  {
    path: 'admin',
    canActivate: [signedInGuard, platformAdminGuard],
    loadChildren: () => import('./admin/admin.routes'),
  },
  { path: '**', redirectTo: 'customer' },
];
