import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../core/api/errors';
import { Order } from '../core/api/models';
import { OwnerOrderApi } from '../core/api/order.api';
import { OrderStatusPipe, isOpen, minutesLeft, nextStepLabel } from '../shared/order-status';
import { OrderView } from '../shared/order-view';
import { pollWhileVisible } from '../shared/poll';

/** B-06: accept or reject with a reason, move through the steps, cancel with a reason. */
@Component({
  selector: 'app-owner-order',
  imports: [RouterLink, OrderStatusPipe, OrderView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/owner/orders">← Orders</a></p>
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }
    @if (order(); as o) {
      <h1>#{{ o.orderNo }} · {{ o.status | orderStatus: 'owner' }}</h1>

      <div class="card customer">
        <strong>{{ o.customerName }}</strong>
        <span>Tower {{ o.tower }}, Flat {{ o.flat }}</span>
        <div class="row">
          <a class="button" [href]="'tel:' + o.customerPhone">Call</a>
          <a
            class="button"
            [href]="'https://wa.me/' + o.customerPhone.replace('+', '')"
            target="_blank"
            rel="noopener"
            >WhatsApp</a
          >
        </div>
      </div>

      <app-order-view [order]="o" view="owner" />

      @if (isOpen(o.status)) {
        <div class="card stack actions">
          @if (o.status === 'Placed') {
            <p class="expires">
              Accept within {{ minutesLeft(o.expiresAt) }} minutes or it is cancelled automatically.
            </p>
            <button type="button" class="primary block" [disabled]="busy()" (click)="act('accept')">
              Accept order
            </button>
          } @else if (next(o)) {
            <button
              type="button"
              class="primary block"
              [disabled]="busy()"
              (click)="act('advance')"
            >
              {{ next(o) }}
            </button>
          }

          <textarea
            #reason
            rows="2"
            maxlength="300"
            placeholder="Reason for the customer"
          ></textarea>
          @if (o.status === 'Placed') {
            <button
              type="button"
              class="danger"
              [disabled]="busy()"
              (click)="act('reject', reason.value)"
            >
              Reject order
            </button>
          } @else {
            <button
              type="button"
              class="danger"
              [disabled]="busy()"
              (click)="act('cancel', reason.value)"
            >
              Cancel order
            </button>
          }
        </div>
      }
    }
  `,
  styles: `
    h1 {
      margin-bottom: 16px;
    }
    .customer {
      display: flex;
      flex-direction: column;
      gap: 4px;
      margin-bottom: 16px;
    }
    .customer .row {
      margin-top: 8px;
    }
    .customer .button {
      flex: 1;
      text-align: center;
      text-decoration: none;
    }
    .actions {
      margin-top: 16px;
    }
    .expires {
      margin: 0;
      color: var(--th-danger);
      font-weight: 600;
    }
  `,
})
export default class OwnerOrder implements OnInit {
  private readonly api = inject(OwnerOrderApi);

  readonly orderId = input.required<string>();

  protected readonly order = signal<Order | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly isOpen = isOpen;
  protected readonly minutesLeft = minutesLeft;

  constructor() {
    // A new order can be cancelled by the customer or expire while the owner looks at it.
    pollWhileVisible(() => this.order()?.status === 'Placed' && this.load());
  }

  ngOnInit(): Promise<void> {
    return this.load();
  }

  protected next(order: Order): string | null {
    return nextStepLabel(order.status);
  }

  protected async act(
    action: 'accept' | 'advance' | 'reject' | 'cancel',
    reason = '',
  ): Promise<void> {
    if ((action === 'reject' || action === 'cancel') && !reason.trim()) {
      this.error.set('Write a reason for the customer first.');
      return;
    }
    const id = this.orderId();
    this.busy.set(true);
    this.error.set(null);
    try {
      this.order.set(
        action === 'accept'
          ? await this.api.accept(id)
          : action === 'advance'
            ? await this.api.advance(id)
            : action === 'reject'
              ? await this.api.reject(id, reason.trim())
              : await this.api.cancel(id, reason.trim()),
      );
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
