import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BusinessAdminApi } from '../core/api/business.api';
import { errorMessage } from '../core/api/errors';
import { BusinessType, Category } from '../core/api/models';

/** A-03: business types and categories, editable without a code change. */
@Component({
  selector: 'app-business-types',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/admin">← Admin</a></p>
    <h1>Types and categories</h1>
    <p class="muted">
      Types with ordering get a menu and orders; the others get a profile and enquiries.
    </p>

    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }

    <div class="stack">
      @for (type of types(); track type.id) {
        <div class="card stack" [class.off]="!type.isActive">
          <div class="row">
            <input
              #typeName
              type="text"
              maxlength="50"
              [value]="type.name"
              aria-label="Type name"
            />
            <label class="check"
              ><input #ordering type="checkbox" [checked]="type.orderingEnabled" /> Ordering</label
            >
            <label class="check"
              ><input #typeActive type="checkbox" [checked]="type.isActive" /> Shown</label
            >
            <button
              type="button"
              [disabled]="busy()"
              (click)="saveType(type, typeName.value, ordering.checked, typeActive.checked)"
            >
              Save
            </button>
          </div>
          <ul class="categories">
            @for (c of type.categories; track c.id) {
              <li class="row" [class.off]="!c.isActive">
                <input
                  #catName
                  type="text"
                  maxlength="50"
                  [value]="c.name"
                  aria-label="Category name"
                />
                <label class="check"
                  ><input #catActive type="checkbox" [checked]="c.isActive" /> Shown</label
                >
                <button
                  type="button"
                  [disabled]="busy()"
                  (click)="saveCategory(c, catName.value, catActive.checked)"
                >
                  Save
                </button>
              </li>
            }
          </ul>
          <form class="row" (submit)="$event.preventDefault(); addCategory(type, newCat)">
            <input #newCat type="text" maxlength="50" placeholder="New category" />
            <button type="submit" [disabled]="busy()">Add category</button>
          </form>
        </div>
      }
    </div>

    <h2>Add a type</h2>
    <form class="card row" (submit)="$event.preventDefault(); addType(newType, newOrdering)">
      <input #newType type="text" maxlength="50" placeholder="Type name" />
      <label class="check"><input #newOrdering type="checkbox" /> Ordering</label>
      <button type="submit" class="primary" [disabled]="busy()">Add type</button>
    </form>
  `,
  styles: `
    .off {
      opacity: 0.7;
    }
    .row > input[type='text'] {
      flex: 1 1 160px;
      width: auto;
    }
    .categories {
      display: flex;
      flex-direction: column;
      gap: 8px;
      margin: 0;
      padding: 0 0 0 12px;
      list-style: none;
      border-left: 2px solid var(--th-border);
    }
  `,
})
export default class BusinessTypes implements OnInit {
  private readonly api = inject(BusinessAdminApi);

  protected readonly types = signal<BusinessType[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): Promise<void> {
    return this.run(() => this.reload());
  }

  protected saveType(
    type: BusinessType,
    name: string,
    orderingEnabled: boolean,
    isActive: boolean,
  ): Promise<void> {
    return this.run(async () => {
      await this.api.updateType(type.id, {
        name,
        orderingEnabled,
        isActive,
        sortOrder: type.sortOrder,
      });
      await this.reload();
    });
  }

  protected saveCategory(category: Category, name: string, isActive: boolean): Promise<void> {
    return this.run(async () => {
      await this.api.updateCategory(category.id, { name, isActive, sortOrder: category.sortOrder });
      await this.reload();
    });
  }

  protected addCategory(type: BusinessType, input: HTMLInputElement): Promise<void> {
    return this.run(async () => {
      const sortOrder = Math.max(0, ...type.categories.map((c) => c.sortOrder)) + 1;
      await this.api.createCategory(type.id, { name: input.value, isActive: true, sortOrder });
      input.value = '';
      await this.reload();
    });
  }

  protected addType(name: HTMLInputElement, ordering: HTMLInputElement): Promise<void> {
    return this.run(async () => {
      const sortOrder = Math.max(0, ...this.types().map((t) => t.sortOrder)) + 1;
      await this.api.createType({
        name: name.value,
        orderingEnabled: ordering.checked,
        isActive: true,
        sortOrder,
      });
      name.value = '';
      ordering.checked = false;
      await this.reload();
    });
  }

  private async reload(): Promise<void> {
    this.types.set(await this.api.types());
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
