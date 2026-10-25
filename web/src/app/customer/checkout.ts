import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { DirectoryApi } from '../core/api/business.api';
import { errorMessage } from '../core/api/errors';
import { BusinessPage, PaymentMethod } from '../core/api/models';
import { OrderApi } from '../core/api/order.api';
import { AuthService } from '../core/auth/auth.service';
import { CartService, MAX_QUANTITY } from '../core/cart/cart.service';
import { RupeesPipe, Time12Pipe } from '../shared/formatting';
import { VegMark } from '../shared/veg-mark';

type When = 'now' | 'today' | 'tomorrow';

/** C-06: note, delivery time, payment method, delivery address prefilled. */
@Component({
  selector: 'app-checkout',
  imports: [ReactiveFormsModule, RouterLink, RupeesPipe, Time12Pipe, VegMark],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (cart.cart(); as c) {
      <p>
        <a [routerLink]="['/customer/b', c.businessId]">← {{ c.businessName }}</a>
      </p>
      <h1>Your order</h1>

      <div class="card stack">
        @for (line of c.lines; track line.itemId) {
          <div class="line">
            <app-veg-mark [isVeg]="line.isVeg" />
            <span class="name">{{ line.name }}</span>
            <div class="stepper">
              <button
                type="button"
                aria-label="Fewer"
                (click)="cart.setQuantity(line.itemId, line.quantity - 1)"
              >
                −
              </button>
              <span>{{ line.quantity }}</span>
              <button
                type="button"
                aria-label="More"
                [disabled]="line.quantity >= max"
                (click)="cart.setQuantity(line.itemId, line.quantity + 1)"
              >
                +
              </button>
            </div>
            <span class="amount">{{ line.price * line.quantity | rupees }}</span>
          </div>
        }
        <div class="totals">
          <span>Items</span><span>{{ cart.subtotal() | rupees }}</span> <span>Delivery</span
          ><span>{{
            (business()?.deliveryCharge ?? 0) > 0 ? (business()!.deliveryCharge | rupees) : 'Free'
          }}</span>
          <strong>Total</strong><strong>{{ total() | rupees }}</strong>
        </div>
        @if (shortBy() > 0) {
          <p class="error">
            Add {{ shortBy() | rupees }} more to reach the minimum order of
            {{ business()!.minOrderValue | rupees }}.
          </p>
        }
      </div>

      <form class="stack card" [formGroup]="form" (ngSubmit)="place()">
        <p class="address">
          Deliver to <strong>Tower {{ profile()?.tower }}, Flat {{ profile()?.flat }}</strong>
        </p>

        <fieldset class="stack">
          <legend>When</legend>
          <div class="choices">
            <label class="choice" [class.on]="when() === 'now'"
              ><input type="radio" value="now" formControlName="when" /> As soon as possible</label
            >
            <label class="choice" [class.on]="when() === 'today'"
              ><input type="radio" value="today" formControlName="when" /> Later today</label
            >
            <label class="choice" [class.on]="when() === 'tomorrow'"
              ><input type="radio" value="tomorrow" formControlName="when" /> Tomorrow</label
            >
          </div>
          @if (when() !== 'now') {
            <label class="field">
              <span
                >Time ({{ business()?.opensAt | time12 }} –
                {{ business()?.closesAt | time12 }})</span
              >
              <input type="time" formControlName="time" />
            </label>
          }
        </fieldset>

        <fieldset class="stack">
          <legend>Payment</legend>
          <div class="choices">
            <label class="choice" [class.on]="payment() === 'Cod'"
              ><input type="radio" value="Cod" formControlName="payment" /> Cash on delivery</label
            >
            @if (business()?.acceptsUpi) {
              <label class="choice" [class.on]="payment() === 'Upi'"
                ><input type="radio" value="Upi" formControlName="payment" /> UPI</label
              >
            }
          </div>
          @if (payment() === 'Upi') {
            <small class="muted"
              >You pay the business directly by UPI after they accept your order.</small
            >
          }
        </fieldset>

        <label class="field">
          <span>Note for the business (optional)</span>
          <textarea
            maxlength="300"
            rows="2"
            placeholder="Less spicy, ring the bell twice…"
            formControlName="note"
          ></textarea>
        </label>

        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
        <button class="primary block" type="submit" [disabled]="busy() || shortBy() > 0">
          Place order · {{ total() | rupees }}
        </button>
      </form>
    } @else {
      <h1>Your cart is empty</h1>
      <p><a routerLink="/customer">Browse businesses</a></p>
    }
  `,
  styles: `
    .line {
      display: grid;
      grid-template-columns: auto 1fr auto auto;
      align-items: center;
      gap: 10px;
    }
    .amount {
      min-width: 64px;
      text-align: right;
    }
    .stepper {
      display: flex;
      align-items: center;
      border: 1px solid var(--th-primary);
      border-radius: 8px;
    }
    .stepper button {
      min-height: 36px;
      width: 32px;
      padding: 0;
      border: 0;
      background: none;
      color: var(--th-primary);
      font-size: 1.125rem;
    }
    .stepper span {
      min-width: 20px;
      text-align: center;
      font-weight: 600;
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
    form {
      margin-top: 16px;
    }
    .address {
      margin: 0;
    }
    fieldset {
      margin: 0;
      padding: 0;
      border: 0;
      gap: 8px;
    }
    legend {
      margin-bottom: 8px;
      font-weight: 600;
      font-size: 0.875rem;
    }
    .choices {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .choice {
      position: relative;
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 8px 12px;
      border: 1px solid var(--th-border);
      border-radius: 999px;
      cursor: pointer;
    }
    .choice.on {
      border-color: var(--th-primary);
      color: var(--th-primary);
      font-weight: 600;
    }
    .choice input {
      position: absolute;
      opacity: 0;
      pointer-events: none;
    }
  `,
})
export default class Checkout implements OnInit {
  protected readonly cart = inject(CartService);
  private readonly auth = inject(AuthService);
  private readonly directory = inject(DirectoryApi);
  private readonly orders = inject(OrderApi);
  private readonly router = inject(Router);

  protected readonly max = MAX_QUANTITY;
  protected readonly profile = computed(() => this.auth.user()?.profile ?? null);
  protected readonly business = signal<BusinessPage | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = new FormGroup({
    when: new FormControl<When>('now', { nonNullable: true }),
    time: new FormControl('', { nonNullable: true }),
    payment: new FormControl<PaymentMethod>('Cod', { nonNullable: true }),
    note: new FormControl('', { nonNullable: true }),
  });
  protected readonly when = toSignal(this.form.controls.when.valueChanges, {
    initialValue: 'now' as When,
  });
  protected readonly payment = toSignal(this.form.controls.payment.valueChanges, {
    initialValue: 'Cod' as PaymentMethod,
  });

  protected readonly total = computed(
    () => this.cart.subtotal() + (this.business()?.deliveryCharge ?? 0),
  );
  protected readonly shortBy = computed(() =>
    Math.max(0, (this.business()?.minOrderValue ?? 0) - this.cart.subtotal()),
  );

  async ngOnInit(): Promise<void> {
    const cart = this.cart.cart();
    if (!cart) {
      return;
    }
    try {
      this.business.set(await this.directory.get(cart.businessId));
    } catch (error) {
      this.error.set(errorMessage(error));
    }
  }

  protected async place(): Promise<void> {
    const cart = this.cart.cart();
    if (!cart) {
      return;
    }
    const v = this.form.getRawValue();
    if (v.when !== 'now' && !v.time) {
      this.error.set('Choose a delivery time.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      const order = await this.orders.place({
        businessId: cart.businessId,
        items: cart.lines.map((l) => ({ itemId: l.itemId, quantity: l.quantity })),
        note: v.note.trim() || null,
        preferredDay: v.when === 'now' ? null : v.when,
        preferredTime: v.when === 'now' ? null : v.time,
        paymentMethod: v.payment,
        idempotencyKey: cart.checkoutKey,
      });
      this.cart.clear();
      await this.router.navigate(['/customer/orders', order.id], { replaceUrl: true });
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
