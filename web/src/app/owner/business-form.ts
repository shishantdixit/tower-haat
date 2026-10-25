import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { BusinessTypeApi, OwnerApi } from '../core/api/business.api';
import { errorMessage } from '../core/api/errors';
import { BusinessType, SaveBusinessRequest } from '../core/api/models';
import { OwnerStore } from './owner-store';

/** B-01: register a business, or edit its details. The kind of business is chosen once. */
@Component({
  selector: 'app-business-form',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/owner">← My business</a></p>
    <h1>{{ isEdit() ? 'Business details' : 'Start selling' }}</h1>

    <form class="stack card" [formGroup]="form" (ngSubmit)="save()">
      @if (!isEdit()) {
        <fieldset class="types">
          <legend>What do you offer?</legend>
          @for (type of types(); track type.id) {
            <label class="type" [class.on]="form.controls.businessTypeId.value === type.id">
              <input type="radio" formControlName="businessTypeId" [value]="type.id" />
              <strong>{{ type.name }}</strong>
              <span class="muted">{{
                type.orderingEnabled ? 'Menu and orders' : 'Profile and enquiries'
              }}</span>
            </label>
          }
        </fieldset>
      }

      @if (selectedType(); as type) {
        <label class="field">
          <span>Category</span>
          <select formControlName="categoryId">
            <option value="" disabled>Choose</option>
            @for (c of type.categories; track c.id) {
              <option [value]="c.id">{{ c.name }}</option>
            }
          </select>
        </label>
      }

      <label class="field">
        <span>Business name</span>
        <input type="text" maxlength="80" placeholder="Asha's Kitchen" formControlName="name" />
      </label>

      <label class="field">
        <span>About your business</span>
        <textarea
          maxlength="1000"
          placeholder="What you make, what is special, how to order"
          formControlName="description"
        ></textarea>
      </label>

      @if (selectedType()?.orderingEnabled) {
        <label class="field">
          <span>UPI ID</span>
          <input
            type="text"
            autocapitalize="off"
            placeholder="name@okaxis"
            formControlName="upiId"
          />
          <small class="muted">Customers pay you directly; TowerHaat never handles money.</small>
        </label>
      }

      @if (selectedType()?.name === 'Food') {
        <label class="field">
          <span>FSSAI number (optional)</span>
          <input type="text" inputmode="numeric" maxlength="17" formControlName="fssaiNumber" />
        </label>
      }

      <label class="field">
        <span>WhatsApp number</span>
        <input
          type="tel"
          inputmode="numeric"
          [placeholder]="'Leave empty to use ' + phone()"
          formControlName="whatsAppPhone"
        />
        <small class="muted">Shown on your page so customers can message you.</small>
      </label>

      @if (error()) {
        <p class="error" role="alert">{{ error() }}</p>
      }

      <button class="primary block" type="submit" [disabled]="busy()">
        {{ isEdit() ? 'Save' : 'Send for approval' }}
      </button>
    </form>
  `,
  styles: `
    fieldset {
      margin: 0;
      padding: 0;
      border: 0;
    }
    legend {
      margin-bottom: 8px;
      font-weight: 600;
      font-size: 0.875rem;
    }
    .types {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
      gap: 8px;
    }
    .type {
      display: flex;
      flex-direction: column;
      padding: 10px 12px;
      border: 1px solid var(--th-border);
      border-radius: 8px;
      cursor: pointer;
    }
    .type.on {
      border-color: var(--th-primary);
      outline: 1px solid var(--th-primary);
    }
    .type input {
      position: absolute;
      opacity: 0;
      pointer-events: none;
    }
    .type .muted {
      font-size: 0.8125rem;
    }
    small {
      font-size: 0.8125rem;
    }
  `,
})
export default class BusinessForm implements OnInit {
  private readonly typeApi = inject(BusinessTypeApi);
  private readonly api = inject(OwnerApi);
  private readonly store = inject(OwnerStore);
  private readonly router = inject(Router);

  readonly mode = input<'register' | 'edit'>('register');
  protected readonly isEdit = computed(() => this.mode() === 'edit');

  protected readonly form = new FormGroup({
    businessTypeId: new FormControl('', { nonNullable: true }),
    categoryId: new FormControl('', { nonNullable: true }),
    name: new FormControl('', { nonNullable: true }),
    description: new FormControl('', { nonNullable: true }),
    upiId: new FormControl('', { nonNullable: true }),
    fssaiNumber: new FormControl('', { nonNullable: true }),
    whatsAppPhone: new FormControl('', { nonNullable: true }),
  });

  protected readonly types = signal<BusinessType[]>([]);
  protected readonly phone = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly typeId = toSignal(this.form.controls.businessTypeId.valueChanges, {
    initialValue: '',
  });
  protected readonly selectedType = computed(
    () => this.types().find((t) => t.id === this.typeId()) ?? null,
  );

  constructor() {
    this.form.controls.businessTypeId.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.form.controls.categoryId.setValue(''));
  }

  async ngOnInit(): Promise<void> {
    try {
      const [types, business] = await Promise.all([this.typeApi.list(), this.store.load()]);
      this.types.set(types);

      if (this.isEdit() && business) {
        this.phone.set(business.whatsAppPhone);
        this.form.patchValue({ businessTypeId: business.businessTypeId });
        this.form.patchValue({
          categoryId: business.categoryId,
          name: business.name,
          description: business.description ?? '',
          upiId: business.upiId ?? '',
          fssaiNumber: business.fssaiNumber ?? '',
          whatsAppPhone: business.whatsAppPhone,
        });
      } else if (business) {
        await this.router.navigateByUrl('/owner');
      }
    } catch (error) {
      this.error.set(errorMessage(error));
    }
  }

  protected async save(): Promise<void> {
    const v = this.form.getRawValue();
    if (!v.businessTypeId || !v.categoryId || !v.name.trim()) {
      this.error.set('Choose what you offer, a category and a business name.');
      return;
    }

    const request: SaveBusinessRequest = {
      businessTypeId: v.businessTypeId,
      categoryId: v.categoryId,
      name: v.name.trim(),
      description: v.description.trim() || null,
      upiId: v.upiId.trim() || null,
      fssaiNumber: v.fssaiNumber.trim() || null,
      whatsAppPhone: v.whatsAppPhone.trim() || null,
    };

    this.busy.set(true);
    this.error.set(null);
    try {
      this.store.set(
        this.isEdit() ? await this.api.update(request) : await this.api.register(request),
      );
      await this.router.navigateByUrl('/owner');
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
