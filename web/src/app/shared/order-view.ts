import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Order } from '../core/api/models';
import { RupeesPipe } from './formatting';
import { OrderStatusPipe } from './order-status';
import { VegMark } from './veg-mark';

/** Items, totals, delivery details and timeline of an order, shared by customer and owner screens (C-07). */
@Component({
  selector: 'app-order-view',
  imports: [DatePipe, RupeesPipe, OrderStatusPipe, VegMark],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let o = order();
    <div class="card stack">
      @for (line of o.items; track $index) {
        <div class="line">
          <app-veg-mark [isVeg]="line.isVeg" />
          <span>{{ line.quantity }} × {{ line.name }}</span>
          <span>{{ line.lineTotal | rupees }}</span>
        </div>
      }
      <div class="totals">
        <span>Items</span><span>{{ o.subtotal | rupees }}</span> <span>Delivery</span
        ><span>{{ o.deliveryCharge > 0 ? (o.deliveryCharge | rupees) : 'Free' }}</span>
        <strong>Total</strong><strong>{{ o.total | rupees }}</strong>
      </div>
      <p class="small">
        {{ o.paymentMethod === 'Upi' ? 'UPI' : 'Cash on delivery' }} ·
        {{
          o.preferredTime
            ? 'Deliver ' + (o.preferredTime | date: 'EEE d MMM, h:mm a' : '+0530')
            : 'As soon as possible'
        }}
      </p>
      @if (o.note) {
        <p class="small note">“{{ o.note }}”</p>
      }
    </div>

    <h2>Timeline</h2>
    <ol class="timeline">
      @for (step of o.history; track $index) {
        <li>
          <strong>{{ step.status | orderStatus: view() }}</strong>
          <span class="muted"> · {{ step.at | date: 'h:mm a' : '+0530' }}</span>
          @if (step.reason) {
            <div class="reason">{{ step.reason }}</div>
          }
        </li>
      }
    </ol>
  `,
  styles: `
    .line {
      display: grid;
      grid-template-columns: auto 1fr auto;
      align-items: center;
      gap: 10px;
    }
    .totals {
      display: grid;
      grid-template-columns: 1fr auto;
      gap: 4px;
      padding-top: 12px;
      border-top: 1px solid var(--th-border);
    }
    .totals > :nth-child(even) {
      text-align: right;
    }
    .small {
      margin: 0;
      font-size: 0.875rem;
    }
    .note {
      font-style: italic;
    }
    .timeline {
      margin: 0;
      padding-left: 20px;
    }
    .timeline li {
      margin-bottom: 8px;
    }
    .reason {
      color: var(--th-danger);
      font-size: 0.875rem;
    }
  `,
})
export class OrderView {
  readonly order = input.required<Order>();
  readonly view = input<'customer' | 'owner'>('customer');
}
