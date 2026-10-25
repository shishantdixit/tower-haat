import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { OwnerApi } from '../core/api/business.api';
import { errorMessage } from '../core/api/errors';
import { OwnerStore } from './owner-store';

/** B-02: hours, last order time, minimum order and delivery charge. */
@Component({
  selector: 'app-settings-form',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/owner">← My business</a></p>
    <h1>Hours and charges</h1>

    <form class="stack card" [formGroup]="form" (ngSubmit)="save()">
      <div class="row pair">
        <label class="field">
          <span>Opens at</span>
          <input type="time" formControlName="opensAt" />
        </label>
        <label class="field">
          <span>Closes at</span>
          <input type="time" formControlName="closesAt" />
        </label>
      </div>

      @if (store.business()?.orderingEnabled) {
        <label class="field">
          <span>Last order time (optional)</span>
          <input type="time" formControlName="acceptOrdersUntil" />
          <small class="muted"
            >Stop taking orders before closing, for example to finish cooking.</small
          >
        </label>
        <div class="row pair">
          <label class="field">
            <span>Minimum order (₹)</span>
            <input
              type="number"
              inputmode="decimal"
              min="0"
              step="1"
              formControlName="minOrderValue"
            />
          </label>
          <label class="field">
            <span>Delivery charge (₹)</span>
            <input
              type="number"
              inputmode="decimal"
              min="0"
              step="1"
              formControlName="deliveryCharge"
            />
          </label>
        </div>
      }

      @if (error()) {
        <p class="error" role="alert">{{ error() }}</p>
      }
      <button class="primary block" type="submit" [disabled]="busy()">Save</button>
    </form>
  `,
  styles: `
    .pair > .field {
      flex: 1 1 130px;
    }
    small {
      font-size: 0.8125rem;
    }
  `,
})
export default class SettingsForm implements OnInit {
  protected readonly store = inject(OwnerStore);
  private readonly api = inject(OwnerApi);
  private readonly router = inject(Router);

  protected readonly form = new FormGroup({
    opensAt: new FormControl('08:00', { nonNullable: true }),
    closesAt: new FormControl('21:00', { nonNullable: true }),
    acceptOrdersUntil: new FormControl('', { nonNullable: true }),
    minOrderValue: new FormControl(0, { nonNullable: true }),
    deliveryCharge: new FormControl(0, { nonNullable: true }),
  });
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    const business = await this.store.load();
    if (!business) {
      await this.router.navigateByUrl('/owner');
      return;
    }
    const s = business.settings;
    this.form.setValue({
      opensAt: s.opensAt,
      closesAt: s.closesAt,
      acceptOrdersUntil: s.acceptOrdersUntil ?? '',
      minOrderValue: s.minOrderValue,
      deliveryCharge: s.deliveryCharge,
    });
  }

  protected async save(): Promise<void> {
    const v = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    try {
      this.store.set(
        await this.api.saveSettings({
          opensAt: v.opensAt,
          closesAt: v.closesAt,
          acceptOrdersUntil: v.acceptOrdersUntil || null,
          minOrderValue: Number(v.minOrderValue) || 0,
          deliveryCharge: Number(v.deliveryCharge) || 0,
        }),
      );
      await this.router.navigateByUrl('/owner');
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
