import {
  ChangeDetectionStrategy,
  Component,
  OnChanges,
  input,
  output,
  signal,
  inject,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { errorMessage } from '../core/api/errors';
import { Order, UpiPayment } from '../core/api/models';
import { OrderApi } from '../core/api/order.api';
import { RupeesPipe } from '../shared/formatting';

/**
 * C-08: pay the business directly by UPI. The app button opens any UPI app with everything filled
 * in; some apps refuse amounts for personal UPI IDs, so the QR code and the UPI ID to copy are
 * always there too. Then the customer taps "I've paid", optionally with the UTR.
 */
@Component({
  selector: 'app-upi-pay',
  imports: [ReactiveFormsModule, RupeesPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let u = upi();
    <div class="stack">
      <p class="amount">
        Pay <strong>{{ u.amount | rupees }}</strong> to {{ u.payeeName }}
      </p>
      <a class="button primary block" [href]="u.link">Pay with UPI app</a>

      <div class="qr">
        @if (qr()) {
          <img [src]="qr()" width="200" height="200" alt="UPI QR code" />
        }
        <small class="muted">Or scan with any UPI app</small>
      </div>

      <div class="row id">
        <span
          >UPI ID: <strong>{{ u.upiId }}</strong></span
        >
        <button type="button" class="link" (click)="copy(u.upiId)">
          {{ copied() ? 'Copied' : 'Copy' }}
        </button>
      </div>

      <form class="stack paid" [formGroup]="form" (ngSubmit)="markPaid()">
        <label class="field">
          <span>UTR / reference number (optional)</span>
          <input
            type="text"
            inputmode="numeric"
            maxlength="35"
            autocomplete="off"
            formControlName="utr"
          />
          <small class="muted"
            >The 12-digit number in your UPI app's payment details. It helps the business find your
            payment.</small
          >
        </label>
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
        <button type="submit" class="block" [disabled]="busy()">I've paid</button>
      </form>
    </div>
  `,
  styles: `
    .amount {
      margin: 0;
      font-size: 1.0625rem;
    }
    .block {
      text-align: center;
      text-decoration: none;
    }
    .qr {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
    }
    .qr img {
      border: 1px solid var(--th-border);
      border-radius: 8px;
      background: #fff;
    }
    .id {
      justify-content: space-between;
      word-break: break-all;
    }
    .paid {
      padding-top: 12px;
      border-top: 1px solid var(--th-border);
    }
    small {
      font-size: 0.8125rem;
    }
  `,
})
export class UpiPay implements OnChanges {
  private readonly api = inject(OrderApi);

  readonly orderId = input.required<string>();
  readonly upi = input.required<UpiPayment>();
  readonly paid = output<Order>();

  protected readonly qr = signal<string | null>(null);
  protected readonly copied = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({ utr: new FormControl('', { nonNullable: true }) });

  async ngOnChanges(): Promise<void> {
    try {
      // Loaded on demand: only customers paying by UPI need the QR library.
      // qrcode is CommonJS, so the bundler puts its functions under default.
      const module = await import('qrcode');
      const QRCode = (module as unknown as { default?: typeof module }).default ?? module;
      this.qr.set(await QRCode.toDataURL(this.upi().link, { width: 400, margin: 1 }));
    } catch {
      this.qr.set(null);
    }
  }

  protected async copy(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch {
      // Clipboard can be blocked; the ID is on screen to copy by hand.
    }
  }

  protected async markPaid(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      this.paid.emit(
        await this.api.markPaid(this.orderId(), this.form.controls.utr.value.trim() || null),
      );
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
