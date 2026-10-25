import { Routes } from '@angular/router';

// One lazy chunk per area; role guards are added with login in M1.
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'customer' },
  { path: 'customer', loadChildren: () => import('./customer/customer.routes') },
  { path: 'owner', loadChildren: () => import('./owner/owner.routes') },
  { path: 'admin', loadChildren: () => import('./admin/admin.routes') },
  { path: '**', redirectTo: 'customer' },
];
