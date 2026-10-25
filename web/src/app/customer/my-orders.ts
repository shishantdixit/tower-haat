import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RealtimeService } from '../core/realtime/realtime.service';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../core/api/errors';
import { OrderSummary } from '../core/api/models';
import { OrderApi } from '../core/api/order.api';
import { RupeesPipe } from '../shared/formatting';
import { OrderStatusPipe, isOpen } from '../shared/order-status';

/** The customer's recent orders, open ones on top. Reorder arrives with order history in M7. */
@Component({
  selector: 'app-my-orders',
  imports: [RouterLink, DatePipe, RupeesPipe, OrderStatusPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>My orders</h1>
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }
    @if (orders(); as list) {
      @if (list.length === 0) {
        <p class="muted">No orders yet. <a routerLink="/customer">Find something to eat</a>.</p>
      }
      <div class="stack">
        @for (o of list; track o.id) {
          <a class="card order" [class.open]="isOpen(o.status)" [routerLink]="o.id">
            <div class="row top">
              <strong>{{ o.businessName }}</strong>
              <span>{{ o.total | rupees }}</span>
            </div>
            <span class="muted small">{{ o.itemsText }}</span>
            <span class="row small">
              <span class="status">{{ o.status | orderStatus }}</span>
              @if (
                o.paymentMethod === 'Upi' &&
                o.paymentStatus === 'Pending' &&
                o.status !== 'Placed' &&
                isOpen(o.status)
              ) {
                <span class="pay">· Pay by UPI</span>
              }
              <span class="muted">· {{ o.placedAt | date: 'd MMM, h:mm a' : '+0530' }}</span>
            </span>
          </a>
        }
      </div>
    }
  `,
  styles: `
    .order {
      display: flex;
      flex-direction: column;
      gap: 4px;
      color: inherit;
      text-decoration: none;
    }
    .order.open {
      border-color: var(--th-primary);
    }
    .top {
      justify-content: space-between;
    }
    .small {
      font-size: 0.875rem;
    }
    .pay {
      color: var(--th-danger);
      font-weight: 600;
    }
    .order.open .status {
      color: var(--th-primary);
      font-weight: 600;
    }
  `,
})
export default class MyOrders implements OnInit {
  private readonly api = inject(OrderApi);

  protected readonly orders = signal<OrderSummary[] | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly isOpen = isOpen;

  constructor() {
    inject(RealtimeService)
      .orderChanged.pipe(takeUntilDestroyed())
      .subscribe(() => void this.ngOnInit());
  }

  async ngOnInit(): Promise<void> {
    try {
      this.orders.set(await this.api.list());
    } catch (error) {
      this.error.set(errorMessage(error));
    }
  }
}
