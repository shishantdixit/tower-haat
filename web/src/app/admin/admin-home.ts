import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-admin-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Admin</h1>
    <p>Societies, approvals and categories arrive in M1 and M2.</p>
  `,
})
export default class AdminHome {}
