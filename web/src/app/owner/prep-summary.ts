import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../core/api/errors';
import { PrepSummary as Summary } from '../core/api/models';
import { OwnerOrderApi } from '../core/api/order.api';
import { pollWhileVisible } from '../shared/poll';
import { VegMark } from '../shared/veg-mark';

/** B-07: item-wise totals across today's open orders. */
@Component({
  selector: 'app-prep-summary',
  imports: [RouterLink, VegMark],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/owner/orders">← Orders</a></p>
    <h1>Prep summary</h1>
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }
    @if (summary(); as s) {
      <p class="muted">
        {{ s.ordersToPrepare }} accepted order{{ s.ordersToPrepare === 1 ? '' : 's' }} to prepare
        today
        @if (s.ordersAwaitingAcceptance) {
          · {{ s.ordersAwaitingAcceptance }} waiting for you to accept
        }
      </p>
      @if (s.items.length === 0) {
        <p>Nothing to prepare right now.</p>
      } @else {
        <table class="card">
          <thead>
            <tr>
              <th>Item</th>
              <th>To make</th>
              <th>If accepted</th>
            </tr>
          </thead>
          <tbody>
            @for (line of s.items; track line.name) {
              <tr>
                <td><app-veg-mark [isVeg]="line.isVeg" /> {{ line.name }}</td>
                <td class="num">
                  <strong>{{ line.toPrepare }}</strong>
                </td>
                <td class="num muted">
                  {{ line.awaitingAcceptance ? '+' + line.awaitingAcceptance : '' }}
                </td>
              </tr>
            }
          </tbody>
        </table>
      }
    }
  `,
  styles: `
    table {
      width: 100%;
      border-collapse: collapse;
    }
    th {
      font-size: 0.8125rem;
      text-align: left;
      color: var(--th-muted);
    }
    th:not(:first-child),
    .num {
      text-align: right;
    }
    td,
    th {
      padding: 8px 4px;
      border-bottom: 1px solid var(--th-border);
    }
    tr:last-child td {
      border-bottom: 0;
    }
    td app-veg-mark {
      vertical-align: -2px;
      margin-right: 4px;
    }
    .num strong {
      font-size: 1.125rem;
    }
  `,
})
export default class PrepSummary implements OnInit {
  private readonly api = inject(OwnerOrderApi);

  protected readonly summary = signal<Summary | null>(null);
  protected readonly error = signal<string | null>(null);

  constructor() {
    pollWhileVisible(() => this.load(), 30000);
  }

  ngOnInit(): Promise<void> {
    return this.load();
  }

  private async load(): Promise<void> {
    try {
      this.summary.set(await this.api.prepSummary());
    } catch (error) {
      this.error.set(errorMessage(error));
    }
  }
}
