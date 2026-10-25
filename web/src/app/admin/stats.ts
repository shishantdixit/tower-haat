import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { errorMessage } from '../core/api/errors';
import { RupeesPipe } from '../shared/formatting';

interface PilotStats {
  activeBusinesses: number;
  residents: number;
  customersInPeriod: number;
  days: {
    date: string;
    placed: number;
    delivered: number;
    rejected: number;
    expired: number;
    cancelled: number;
    disputed: number;
  }[];
  businesses: {
    name: string;
    orders: number;
    delivered: number;
    rejectedOrExpired: number;
    deliveredValue: number;
  }[];
}

/** The pilot's numbers: orders a day and how they ended, disputes, and each business. */
@Component({
  selector: 'app-stats',
  imports: [RouterLink, DatePipe, RupeesPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/admin">← Admin</a></p>
    <h1>Pilot stats</h1>
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }
    @if (stats(); as s) {
      <div class="tiles">
        <div class="card">
          <strong>{{ s.activeBusinesses }}</strong
          ><span>active businesses</span>
        </div>
        <div class="card">
          <strong>{{ s.residents }}</strong
          ><span>residents signed up</span>
        </div>
        <div class="card">
          <strong>{{ s.customersInPeriod }}</strong
          ><span>customers in 14 days</span>
        </div>
      </div>

      <h2>Orders by day</h2>
      <div class="table card">
        <table>
          <thead>
            <tr>
              <th>Day</th>
              <th>Orders</th>
              <th>Delivered</th>
              <th>Rejected</th>
              <th>Expired</th>
              <th>Cancelled</th>
              <th>Disputes</th>
            </tr>
          </thead>
          <tbody>
            @for (d of s.days; track d.date) {
              <tr [class.quiet]="d.placed === 0">
                <td>{{ d.date | date: 'EEE d MMM' }}</td>
                <td>{{ d.placed }}</td>
                <td>{{ d.delivered }}</td>
                <td>{{ d.rejected }}</td>
                <td>{{ d.expired }}</td>
                <td>{{ d.cancelled }}</td>
                <td [class.alert]="d.disputed > 0">{{ d.disputed }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <h2>Businesses (14 days)</h2>
      <div class="table card">
        <table>
          <thead>
            <tr>
              <th>Business</th>
              <th>Orders</th>
              <th>Delivered</th>
              <th>Rejected or expired</th>
              <th>Delivered value</th>
            </tr>
          </thead>
          <tbody>
            @for (b of s.businesses; track b.name) {
              <tr>
                <td>{{ b.name }}</td>
                <td>{{ b.orders }}</td>
                <td>{{ b.delivered }}</td>
                <td [class.alert]="b.rejectedOrExpired > 0">{{ b.rejectedOrExpired }}</td>
                <td>{{ b.deliveredValue | rupees }}</td>
              </tr>
            } @empty {
              <tr>
                <td colspan="5" class="muted">No orders yet.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: `
    .tiles {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 8px;
    }
    .tiles .card {
      display: flex;
      flex-direction: column;
      padding: 12px;
    }
    .tiles strong {
      font-size: 1.5rem;
    }
    .tiles span {
      color: var(--th-muted);
      font-size: 0.8125rem;
    }
    .table {
      overflow-x: auto;
      padding: 8px;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.875rem;
    }
    th,
    td {
      padding: 6px;
      text-align: right;
      white-space: nowrap;
    }
    th:first-child,
    td:first-child {
      text-align: left;
    }
    th {
      color: var(--th-muted);
      font-weight: 500;
    }
    tr.quiet {
      color: var(--th-muted);
    }
    .alert {
      color: var(--th-danger);
      font-weight: 600;
    }
  `,
})
export default class Stats implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly stats = signal<PilotStats | null>(null);
  protected readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.stats.set(
        await firstValueFrom(
          this.http.get<PilotStats>('/api/admin/stats', { params: { days: 14 } }),
        ),
      );
    } catch (error) {
      this.error.set(errorMessage(error));
    }
  }
}
