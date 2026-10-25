import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { OwnerApi } from '../core/api/business.api';
import { errorMessage } from '../core/api/errors';
import { OwnerItem, SaveItemRequest } from '../core/api/models';
import { resizeImage } from '../core/media/resize-image';
import { OwnerStore } from './owner-store';

type VegChoice = 'veg' | 'nonveg' | 'na';

/** B-03: add or edit one item. */
@Component({
  selector: 'app-item-form',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/owner/menu">← Menu</a></p>
    <h1>{{ isNew() ? 'Add item' : 'Edit item' }}</h1>

    <form class="stack card" [formGroup]="form" (ngSubmit)="save()">
      <label class="field">
        <span>Name</span>
        <input type="text" maxlength="100" placeholder="Veg thali" formControlName="name" />
      </label>
      <label class="field">
        <span>Description (optional)</span>
        <textarea
          maxlength="500"
          placeholder="Dal, sabzi, 4 rotis, rice"
          formControlName="description"
        ></textarea>
      </label>
      <div class="row pair">
        <label class="field">
          <span>Price (₹)</span>
          <input type="number" inputmode="decimal" min="1" step="1" formControlName="price" />
        </label>
        <label class="field">
          <span>Daily limit (optional)</span>
          <input
            type="number"
            inputmode="numeric"
            min="0"
            step="1"
            placeholder="No limit"
            formControlName="dailyLimit"
          />
        </label>
      </div>

      @if (isFood()) {
        <fieldset class="row veg">
          <label class="check"><input type="radio" value="veg" formControlName="veg" /> Veg</label>
          <label class="check"
            ><input type="radio" value="nonveg" formControlName="veg" /> Non-veg</label
          >
        </fieldset>
      }

      <label class="check">
        <input type="checkbox" formControlName="isAvailableToday" />
        <span>Available today</span>
      </label>
      <label class="check">
        <input type="checkbox" formControlName="isHidden" />
        <span>Hide from customers</span>
      </label>

      @if (!isNew()) {
        <div class="photo">
          @if (item()?.photoUrl) {
            <img [src]="item()!.photoUrl" alt="" />
          }
          <label class="button">
            <input type="file" accept="image/*" [disabled]="busy()" (change)="setPhoto($event)" />
            {{ item()?.photoUrl ? 'Change photo' : 'Add photo' }}
          </label>
          @if (item()?.photoUrl) {
            <button type="button" class="link danger" [disabled]="busy()" (click)="removePhoto()">
              Remove photo
            </button>
          }
        </div>
      }

      @if (error()) {
        <p class="error" role="alert">{{ error() }}</p>
      }
      <button class="primary block" type="submit" [disabled]="busy()">
        {{ isNew() ? 'Add and continue' : 'Save' }}
      </button>
      @if (!isNew()) {
        <button type="button" class="danger" [disabled]="busy()" (click)="remove()">
          Delete item
        </button>
      }
    </form>
  `,
  styles: `
    .pair > .field {
      flex: 1 1 130px;
    }
    fieldset {
      margin: 0;
      padding: 0;
      border: 0;
      gap: 24px;
    }
    .photo {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px;
    }
    .photo img {
      width: 96px;
      height: 96px;
      border-radius: 8px;
      object-fit: cover;
    }
    .photo label {
      position: relative;
      display: inline-flex;
      align-items: center;
    }
    .photo input {
      position: absolute;
      inset: 0;
      opacity: 0;
      cursor: pointer;
    }
  `,
})
export default class ItemForm implements OnInit {
  private readonly api = inject(OwnerApi);
  private readonly store = inject(OwnerStore);
  private readonly router = inject(Router);

  readonly itemId = input<string>();
  protected readonly isNew = computed(() => !this.itemId());
  protected readonly isFood = computed(() => this.store.business()?.businessTypeName === 'Food');

  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true }),
    description: new FormControl('', { nonNullable: true }),
    price: new FormControl<number | null>(null),
    dailyLimit: new FormControl<number | null>(null),
    veg: new FormControl<VegChoice>('veg', { nonNullable: true }),
    isAvailableToday: new FormControl(true, { nonNullable: true }),
    isHidden: new FormControl(false, { nonNullable: true }),
  });

  protected readonly item = signal<OwnerItem | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      await this.store.load();
      if (!this.isFood()) {
        this.form.controls.veg.setValue('na');
      }
      const id = this.itemId();
      if (id) {
        const item = (await this.api.items()).find((i) => i.id === id);
        if (!item) {
          await this.router.navigateByUrl('/owner/menu');
          return;
        }
        this.item.set(item);
        this.form.setValue({
          name: item.name,
          description: item.description ?? '',
          price: item.price,
          dailyLimit: item.dailyLimit,
          veg: item.isVeg === null ? 'na' : item.isVeg ? 'veg' : 'nonveg',
          isAvailableToday: item.isAvailableToday,
          isHidden: item.isHidden,
        });
      }
    } catch (error) {
      this.error.set(errorMessage(error));
    }
  }

  protected async save(): Promise<void> {
    const v = this.form.getRawValue();
    if (!v.name.trim() || !v.price || v.price <= 0) {
      this.error.set('Enter a name and a price.');
      return;
    }
    const request: SaveItemRequest = {
      name: v.name.trim(),
      description: v.description.trim() || null,
      price: Number(v.price),
      isVeg: v.veg === 'na' ? null : v.veg === 'veg',
      dailyLimit:
        v.dailyLimit === null || (v.dailyLimit as unknown) === '' ? null : Number(v.dailyLimit),
      isAvailableToday: v.isAvailableToday,
      isHidden: v.isHidden,
      sortOrder: this.item()?.sortOrder ?? 0,
    };

    await this.run(async () => {
      const id = this.itemId();
      if (id) {
        this.item.set(await this.api.updateItem(id, request));
        await this.router.navigateByUrl('/owner/menu');
      } else {
        // Stay on the item after adding it, so a photo can be added next.
        const created = await this.api.createItem(request);
        await this.router.navigate(['/owner/menu', created.id], { replaceUrl: true });
      }
    });
  }

  protected async setPhoto(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (file) {
      await this.run(async () =>
        this.item.set(await this.api.setItemPhoto(this.itemId()!, await resizeImage(file))),
      );
    }
  }

  protected removePhoto(): Promise<void> {
    return this.run(async () => this.item.set(await this.api.removeItemPhoto(this.itemId()!)));
  }

  protected async remove(): Promise<void> {
    if (!confirm(`Delete ${this.item()?.name}?`)) {
      return;
    }
    await this.run(async () => {
      await this.api.deleteItem(this.itemId()!);
      await this.router.navigateByUrl('/owner/menu');
    });
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
    } catch (error) {
      // Resize problems are plain errors; API failures are HTTP errors.
      this.error.set(error instanceof Error ? error.message : errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
