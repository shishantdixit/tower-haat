import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../core/api/errors';
import { Order, PaymentAction } from '../core/api/models';
import { OwnerOrderApi } from '../core/api/order.api';
import { OrderStatusPipe, isOpen, minutesLeft, nextStepLabel } from '../shared/order-status';
import { OrderView } from '../shared/order-view';
import { PaymentLabelPipe, isSettled } from '../shared/payment-status';
import { pollWhileVisible } from '../shared/poll';

/** B-06: accept or reject with a reason, move through the steps, cancel with a reason. B-08: payment. */
@Component({
  selector: 'app-owner-order',
  imports: [RouterLink, OrderStatusPipe, OrderView, PaymentLabelPipe],
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

      @let payment = o.payment;
      <div class="card stack payment">
        <h2>Payment</h2>
        <p>
          {{ o.paymentMethod === 'Upi' ? 'UPI' : 'Cash' }} ·
          <strong>{{ payment.status | paymentLabel: o.paymentMethod : 'owner' }}</strong>
          @if (payment.utr) {
            <br /><span class="muted">UTR {{ payment.utr }}</span>
          }
        </p>
        @if (canConfirmUpi(o)) {
          <button type="button" class="primary" [disabled]="busy()" (click)="pay('Confirm')">
            UPI received
          </button>
        }
        @if (o.paymentMethod === 'Upi' && payment.status === 'CustomerMarkedPaid') {
          <textarea
            #disputeNote
            rows="2"
            maxlength="300"
            placeholder="Note for the customer (optional)"
          ></textarea>
          <button
            type="button"
            class="danger"
            [disabled]="busy()"
            (click)="pay('Dispute', disputeNote.value)"
          >
            Not received
          </button>
        }
        @if (
          o.paymentMethod === 'Cod' &&
          payment.status === 'Pending' &&
          (o.status === 'OutForDelivery' || o.status === 'Delivered')
        ) {
          <button type="button" class="primary" [disabled]="busy()" (click)="pay('Collect')">
            Cash collected
          </button>
        }
        @if (canRefund(o)) {
          <button type="button" class="link" [disabled]="busy()" (click)="pay('Refund')">
            I refunded this payment
          </button>
        }
      </div>

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
    .actions,
    .payment {
      margin-top: 16px;
    }
    .payment h2,
    .payment p {
      margin: 0;
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
    pollWhileVisible(() => {
      const order = this.order();
      return (
        order &&
        (order.status === 'Placed' ||
          (order.paymentMethod === 'Upi' &&
            order.payment.status === 'Pending' &&
            isOpen(order.status))) &&
        this.load()
      );
    });
  }

  ngOnInit(): Promise<void> {
    return this.load();
  }

  protected canConfirmUpi(order: Order): boolean {
    return (
      order.paymentMethod === 'Upi' &&
      ['Pending', 'CustomerMarkedPaid', 'Disputed'].includes(order.payment.status) &&
      !['Placed', 'Rejected', 'Expired', 'CancelledByCustomer'].includes(order.status)
    );
  }

  protected canRefund(order: Order): boolean {
    return (
      isSettled(order.payment.status) ||
      order.payment.status === 'CustomerMarkedPaid' ||
      order.payment.status === 'Disputed'
    );
  }

  protected async pay(action: PaymentAction, note = ''): Promise<void> {
    if (action === 'Refund' && !confirm('Mark this payment as refunded to the customer?')) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    try {
      this.order.set(await this.api.payment(this.orderId(), action, note.trim() || null));
    } catch (error) {
      this.error.set(errorMessage(error));
      await this.load();
    } finally {
      this.busy.set(false);
    }
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
