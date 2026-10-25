import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RealtimeService } from '../core/realtime/realtime.service';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../core/api/errors';
import { OwnerOrderTab, OwnerOrders } from '../core/api/models';
import { OwnerOrderApi } from '../core/api/order.api';
import { RupeesPipe } from '../shared/formatting';
import { OrderStatusPipe, minutesLeft } from '../shared/order-status';
import { PaymentLabelPipe } from '../shared/payment-status';
import { pollWhileVisible } from '../shared/poll';

const TABS: { tab: OwnerOrderTab; label: string }[] = [
  { tab: 'New', label: 'New' },
  { tab: 'Preparing', label: 'Preparing' },
  { tab: 'OutForDelivery', label: 'Delivering' },
  { tab: 'Done', label: 'Done' },
];

/** B-05: orders by status, updated live; a slow refresh covers dropped connections. */
@Component({
  selector: 'app-owner-orders',
  imports: [RouterLink, DatePipe, RupeesPipe, OrderStatusPipe, PaymentLabelPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/owner">← My business</a></p>
    <div class="row head">
      <h1>Orders</h1>
      <a routerLink="/owner/prep">Prep summary</a>
    </div>

    <div class="tabs" role="tablist">
      @for (t of tabs; track t.tab) {
        <button type="button" role="tab" [class.on]="tab() === t.tab" (click)="show(t.tab)">
          {{ t.label }}
          @if (data()?.counts?.[t.tab]; as count) {
            <span class="count" [class.alert]="t.tab === 'New'">{{ count }}</span>
          }
        </button>
      }
    </div>

    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }

    @if (data(); as d) {
      @if (d.orders.length === 0) {
        <p class="muted">
          {{
            tab() === 'New' ? 'No new orders. They appear here as they come in.' : 'Nothing here.'
          }}
        </p>
      }
      <div class="stack">
        @for (o of d.orders; track o.id) {
          <a class="card order" [class.new]="o.status === 'Placed'" [routerLink]="o.id">
            <div class="row top">
              <strong>#{{ o.orderNo }} · {{ o.customerName }}</strong>
              <span>{{ o.total | rupees }}</span>
            </div>
            <span>Tower {{ o.tower }}, Flat {{ o.flat }}</span>
            <span class="muted small">{{ o.itemsText }}</span>
            <span class="small">
              {{ o.placedAt | date: 'h:mm a' : '+0530' }} ·
              {{ o.paymentStatus | paymentLabel: o.paymentMethod : 'owner' }}
              @if (o.preferredTime) {
                · <strong>for {{ o.preferredTime | date: 'EEE h:mm a' : '+0530' }}</strong>
              }
              @if (o.status === 'Placed') {
                · <span class="expires">accept within {{ minutesLeft(o.expiresAt) }} min</span>
              } @else if (tab() === 'Done') {
                · {{ o.status | orderStatus: 'owner' }}
              }
            </span>
          </a>
        }
      </div>
    }
  `,
  styles: `
    .head {
      justify-content: space-between;
    }
    .tabs {
      display: flex;
      gap: 6px;
      overflow-x: auto;
      margin-bottom: 16px;
    }
    .tabs button {
      flex-shrink: 0;
      min-height: 40px;
      border-radius: 999px;
      font-weight: 500;
    }
    .tabs button.on {
      border-color: var(--th-primary);
      background: var(--th-primary);
      color: var(--th-primary-contrast);
    }
    .count {
      margin-left: 4px;
      padding: 0 7px;
      border-radius: 999px;
      background: var(--th-border);
      color: var(--th-text);
      font-size: 0.8125rem;
    }
    .count.alert {
      background: var(--th-danger);
      color: #fff;
    }
    .order {
      display: flex;
      flex-direction: column;
      gap: 2px;
      color: inherit;
      text-decoration: none;
    }
    .order.new {
      border-color: var(--th-primary);
      background: #fff7ed;
    }
    .top {
      justify-content: space-between;
    }
    .small {
      font-size: 0.875rem;
    }
    .expires {
      color: var(--th-danger);
      font-weight: 600;
    }
  `,
})
export default class Orders implements OnInit {
  private readonly api = inject(OwnerOrderApi);

  protected readonly tabs = TABS;
  protected readonly tab = signal<OwnerOrderTab>('New');
  protected readonly data = signal<OwnerOrders | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly minutesLeft = minutesLeft;

  constructor() {
    inject(RealtimeService)
      .orderChanged.pipe(takeUntilDestroyed())
      .subscribe(() => void this.load());
    pollWhileVisible(() => this.load(), 60000);
  }

  ngOnInit(): Promise<void> {
    return this.load();
  }

  protected show(tab: OwnerOrderTab): Promise<void> {
    this.tab.set(tab);
    this.data.set(null);
    return this.load();
  }

  private async load(): Promise<void> {
    try {
      this.data.set(await this.api.list(this.tab()));
      this.error.set(null);
    } catch (error) {
      this.error.set(errorMessage(error));
    }
  }
}
