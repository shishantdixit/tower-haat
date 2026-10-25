import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BusinessAdminApi } from '../core/api/business.api';

@Component({
  selector: 'app-admin-home',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Admin</h1>
    <nav class="links">
      <a routerLink="businesses">
        Business approvals
        <span class="muted">{{ pending() === null ? '' : pending() + ' waiting' }}</span>
      </a>
      <a routerLink="types"
        >Types and categories <span class="muted">What businesses can offer</span></a
      >
      <a routerLink="societies">Societies <span class="muted">Towers and flats</span></a>
      <a routerLink="stats"
        >Pilot stats <span class="muted">Orders a day, rejections, disputes</span></a
      >
      <a routerLink="backups">Backups <span class="muted">Download a copy of the database</span></a>
      <a routerLink="errors">Error log <span class="muted">Recent warnings and errors</span></a>
    </nav>
  `,
  styles: `
    .links {
      display: flex;
      flex-direction: column;
      border: 1px solid var(--th-border);
      border-radius: var(--th-radius);
      background: var(--th-surface);
    }
    .links a {
      display: flex;
      flex-direction: column;
      padding: 12px var(--th-gap);
      color: var(--th-text);
      font-weight: 600;
      text-decoration: none;
    }
    .links a + a {
      border-top: 1px solid var(--th-border);
    }
    .links .muted {
      font-size: 0.875rem;
      font-weight: 400;
    }
  `,
})
export default class AdminHome implements OnInit {
  private readonly api = inject(BusinessAdminApi);
  protected readonly pending = signal<number | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.pending.set((await this.api.list('Pending')).length);
    } catch {
      // The count is a convenience; the page works without it.
    }
  }
}
