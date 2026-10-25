import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { OwnerApi } from '../core/api/business.api';
import { errorMessage } from '../core/api/errors';
import { OwnerItem } from '../core/api/models';
import { RupeesPipe } from '../shared/formatting';
import { VegMark } from '../shared/veg-mark';

/** B-03: the owner's menu, with a one-tap "available today" switch per item. */
@Component({
  selector: 'app-menu',
  imports: [RouterLink, RupeesPipe, VegMark],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/owner">← My business</a></p>
    <div class="row head">
      <h1>Menu</h1>
      <a class="button primary" routerLink="new">+ Add item</a>
    </div>

    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }

    @if (items(); as list) {
      @if (list.length === 0) {
        <p class="muted">No items yet. Add your first dish or product.</p>
      }
      <div class="stack">
        @for (item of list; track item.id) {
          <div class="card item" [class.off]="item.isHidden || !item.isAvailableToday">
            <a class="text" [routerLink]="item.id">
              <span class="row name">
                <app-veg-mark [isVeg]="item.isVeg" />
                <strong>{{ item.name }}</strong>
              </span>
              <span
                >{{ item.price | rupees }}
                @if (item.dailyLimit !== null) {
                  <span class="muted"> · {{ item.dailyLimit }} a day</span>
                }
              </span>
              @if (item.isHidden) {
                <span class="tag">Hidden</span>
              }
            </a>
            <label class="check today">
              <input
                type="checkbox"
                [checked]="item.isAvailableToday"
                [disabled]="busy() === item.id"
                (change)="setAvailable(item, $any($event.target).checked)"
              />
              <span>Today</span>
            </label>
          </div>
        }
      </div>
    }
  `,
  styles: `
    .head {
      justify-content: space-between;
    }
    .head a {
      text-decoration: none;
    }
    .item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding: 12px;
    }
    .item.off .text {
      opacity: 0.6;
    }
    .text {
      display: flex;
      flex-direction: column;
      gap: 2px;
      color: inherit;
      text-decoration: none;
      min-width: 0;
    }
    .name {
      flex-wrap: nowrap;
    }
    .tag {
      align-self: flex-start;
      padding: 1px 8px;
      border-radius: 999px;
      background: var(--th-border);
      font-size: 0.75rem;
    }
    .today {
      align-items: center;
      font-size: 0.875rem;
    }
  `,
})
export default class Menu implements OnInit {
  private readonly api = inject(OwnerApi);

  protected readonly items = signal<OwnerItem[] | null>(null);
  protected readonly busy = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.items.set(await this.api.items());
    } catch (error) {
      this.error.set(errorMessage(error));
    }
  }

  protected async setAvailable(item: OwnerItem, available: boolean): Promise<void> {
    this.busy.set(item.id);
    this.error.set(null);
    try {
      const updated = await this.api.setAvailable(item.id, available);
      this.items.update((list) => list?.map((i) => (i.id === updated.id ? updated : i)) ?? null);
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.busy.set(null);
    }
  }
}
