import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DirectoryApi } from '../core/api/business.api';
import { errorMessage } from '../core/api/errors';
import { BusinessPage as Page } from '../core/api/models';
import { RupeesPipe, Time12Pipe } from '../shared/formatting';
import { VegMark } from '../shared/veg-mark';

/** C-03 business page and C-04 menu with sold-out state. Ordering arrives in M3. */
@Component({
  selector: 'app-business-page',
  imports: [RouterLink, RupeesPipe, Time12Pipe, VegMark],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/customer">← All businesses</a></p>
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }

    @if (page(); as p) {
      @if (p.isOwnerPreview) {
        <p class="card notice">
          Preview: customers will see this page once your business is approved.
        </p>
      }

      @if (p.photoUrls.length) {
        <div class="photos">
          @for (url of p.photoUrls; track url) {
            <img [src]="url" alt="" loading="lazy" />
          }
        </div>
      }

      <h1>{{ p.name }}</h1>
      <p class="muted">{{ p.categoryName }}</p>
      <p class="row">
        @if (p.isOpenNow) {
          <span class="badge open">Open now</span>
        } @else {
          <span class="badge">Closed</span>
        }
        <span class="muted">
          {{
            p.opensAt === p.closesAt
              ? 'All day'
              : (p.opensAt | time12) + ' – ' + (p.closesAt | time12)
          }}
        </span>
      </p>
      @if (p.description) {
        <p class="description">{{ p.description }}</p>
      }

      @if (p.orderingEnabled) {
        <ul class="facts muted">
          @if (p.acceptOrdersUntil) {
            <li>Orders until {{ p.acceptOrdersUntil | time12 }}</li>
          }
          @if (p.minOrderValue > 0) {
            <li>Minimum order {{ p.minOrderValue | rupees }}</li>
          }
          <li>
            {{
              p.deliveryCharge > 0
                ? 'Delivery ' + (p.deliveryCharge | rupees)
                : 'Free delivery to your flat'
            }}
          </li>
          @if (p.fssaiNumber) {
            <li>FSSAI {{ p.fssaiNumber }}</li>
          }
        </ul>
      }

      <a class="button whatsapp" [href]="p.whatsAppUrl" target="_blank" rel="noopener">
        {{ p.orderingEnabled ? 'Chat on WhatsApp' : 'Enquire on WhatsApp' }}
      </a>

      @if (p.orderingEnabled) {
        <h2>Menu</h2>
        @if (p.items.length === 0) {
          <p class="muted">The menu is being prepared.</p>
        }
        <div class="stack">
          @for (item of p.items; track item.id) {
            <div class="item" [class.soldout]="item.isSoldOut">
              <div class="text">
                <div class="row name">
                  <app-veg-mark [isVeg]="item.isVeg" />
                  <strong>{{ item.name }}</strong>
                </div>
                <span class="price">{{ item.price | rupees }}</span>
                @if (item.description) {
                  <span class="muted small">{{ item.description }}</span>
                }
                @if (item.isSoldOut) {
                  <span class="soldout-label">Sold out</span>
                } @else if (item.remainingToday !== null && item.remainingToday <= 5) {
                  <span class="few">Only {{ item.remainingToday }} left today</span>
                }
              </div>
              @if (item.photoUrl) {
                <img [src]="item.photoUrl" alt="" loading="lazy" />
              }
            </div>
          }
        </div>
      }
    } @else if (!error()) {
      <p class="muted">Loading…</p>
    }
  `,
  styles: `
    .notice {
      background: #fff7ed;
    }
    .photos {
      display: flex;
      gap: 8px;
      overflow-x: auto;
      margin: 0 calc(-1 * var(--th-gap)) 12px;
      padding: 0 var(--th-gap);
      scroll-snap-type: x mandatory;
    }
    .photos img {
      width: 85%;
      max-width: 420px;
      aspect-ratio: 4 / 3;
      object-fit: cover;
      border-radius: var(--th-radius);
      scroll-snap-align: start;
      flex-shrink: 0;
    }
    .photos img:only-child {
      width: 100%;
    }
    h1 + .muted {
      margin-top: 0;
    }
    .badge {
      padding: 2px 10px;
      border-radius: 999px;
      background: var(--th-border);
      font-size: 0.8125rem;
      font-weight: 600;
    }
    .badge.open {
      background: #dcfce7;
      color: var(--th-success);
    }
    .description {
      white-space: pre-line;
    }
    .facts {
      margin: 0 0 16px;
      padding-left: 18px;
      font-size: 0.9375rem;
    }
    .whatsapp {
      display: flex;
      align-items: center;
      justify-content: center;
      border-color: #16a34a;
      color: #15803d;
      text-decoration: none;
    }
    .item {
      display: flex;
      justify-content: space-between;
      gap: 12px;
      padding-bottom: 16px;
      border-bottom: 1px solid var(--th-border);
    }
    .item .text {
      display: flex;
      flex-direction: column;
      gap: 2px;
      min-width: 0;
    }
    .item .name {
      flex-wrap: nowrap;
    }
    .item img {
      width: 96px;
      height: 96px;
      border-radius: 8px;
      object-fit: cover;
      flex-shrink: 0;
    }
    .item.soldout img,
    .item.soldout .price,
    .item.soldout strong {
      opacity: 0.5;
    }
    .price {
      font-weight: 600;
    }
    .small {
      font-size: 0.875rem;
    }
    .soldout-label {
      color: var(--th-danger);
      font-weight: 600;
      font-size: 0.875rem;
    }
    .few {
      color: var(--th-primary);
      font-size: 0.875rem;
    }
  `,
})
export default class BusinessPage implements OnInit {
  private readonly api = inject(DirectoryApi);

  readonly businessId = input.required<string>();

  protected readonly page = signal<Page | null>(null);
  protected readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.page.set(await this.api.get(this.businessId()));
    } catch (error) {
      this.error.set(errorMessage(error));
    }
  }
}
