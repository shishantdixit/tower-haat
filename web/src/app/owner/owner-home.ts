import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-owner-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>My business</h1>
    <p>Orders, catalog and settings arrive in M2 and M3.</p>
  `,
})
export default class OwnerHome {}
