import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../core/api/errors';
import { Order } from '../core/api/models';
import { OrderApi } from '../core/api/order.api';
import { OrderStatusPipe, isOpen, minutesLeft } from '../shared/order-status';
import { OrderView } from '../shared/order-view';
import { pollWhileVisible } from '../shared/poll';

/** C-07 order status and timeline; C-09 cancel while it is still waiting to be accepted. */
@Component({
  selector: 'app-order-detail',
  imports: [RouterLink, OrderStatusPipe, OrderView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/customer/orders">← My orders</a></p>
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }
    @if (order(); as o) {
      <h1>{{ o.businessName }} · #{{ o.orderNo }}</h1>
      <div class="card status" [class.open]="isOpen(o.status)">
        <strong>{{ o.status | orderStatus }}</strong>
        @if (o.status === 'Placed') {
          <p class="muted">
            If {{ o.businessName }} does not accept within {{ minutesLeft(o.expiresAt) }} minutes,
            the order is cancelled automatically.
          </p>
        }
      </div>

      <app-order-view [order]="o" />

      <div class="stack actions">
        <a class="button" [href]="o.businessWhatsAppUrl" target="_blank" rel="noopener"
          >Message on WhatsApp</a
        >
        @if (o.status === 'Placed') {
          <button type="button" class="danger" [disabled]="busy()" (click)="cancel()">
            Cancel order
          </button>
        }
      </div>
    }
  `,
  styles: `
    .status {
      margin-bottom: 16px;
    }
    .status.open {
      border-color: var(--th-primary);
      background: #fff7ed;
    }
    .status p {
      margin: 4px 0 0;
      font-size: 0.875rem;
    }
    .actions {
      margin-top: 16px;
    }
    .actions .button {
      text-align: center;
      text-decoration: none;
    }
  `,
})
export default class OrderDetail implements OnInit {
  private readonly api = inject(OrderApi);

  readonly orderId = input.required<string>();

  protected readonly order = signal<Order | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly isOpen = isOpen;
  protected readonly minutesLeft = minutesLeft;

  constructor() {
    pollWhileVisible(() => {
      const status = this.order()?.status;
      if (status && isOpen(status)) {
        void this.load();
      }
    });
  }

  ngOnInit(): Promise<void> {
    return this.load();
  }

  protected async cancel(): Promise<void> {
    if (!confirm('Cancel this order?')) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    try {
      this.order.set(await this.api.cancel(this.orderId()));
    } catch (error) {
      this.error.set(errorMessage(error));
      await this.load();
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      this.order.set(await this.api.get(this.orderId()));
    } catch (error) {
      this.error.set(errorMessage(error));
    }
  }
}
