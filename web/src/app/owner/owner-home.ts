import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { OwnerApi } from '../core/api/business.api';
import { errorMessage } from '../core/api/errors';
import { OwnerStore } from './owner-store';

/** "My business": start selling, or see status, switch open/closed and manage the business. */
@Component({
  selector: 'app-owner-home',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }

    @if (!loading()) {
      @if (store.business(); as b) {
        <h1>{{ b.name }}</h1>
        <p class="muted">{{ b.businessTypeName }} · {{ b.categoryName }}</p>

        @switch (b.status) {
          @case ('Pending') {
            <div class="card status pending">
              <strong>Waiting for approval</strong>
              <p>
                The TowerHaat team will review your business soon. Meanwhile, add photos{{
                  b.orderingEnabled ? ' and your menu' : ''
                }}.
              </p>
            </div>
          }
          @case ('Rejected') {
            <div class="card status rejected">
              <strong>Changes needed</strong>
              <p>{{ b.statusNote }}</p>
              <a routerLink="profile">Edit and send again</a>
            </div>
          }
          @case ('Deactivated') {
            <div class="card status rejected">
              <strong>Your business is switched off</strong>
              <p>{{ b.statusNote }}</p>
              <p class="muted">Please contact the TowerHaat team.</p>
            </div>
          }
          @case ('Active') {
            <div class="card open-switch">
              <div>
                <strong>{{ b.settings.isOpen ? 'Open for orders' : 'Closed' }}</strong>
                <p class="muted">
                  {{
                    b.settings.isOpen
                      ? b.isOpenNow
                        ? 'Customers can see you are open.'
                        : 'Outside your hours right now.'
                      : 'Customers see you as closed.'
                  }}
                </p>
              </div>
              <button
                type="button"
                [class.primary]="!b.settings.isOpen"
                [disabled]="busy()"
                (click)="toggleOpen(!b.settings.isOpen)"
              >
                {{ b.settings.isOpen ? 'Close now' : 'Open now' }}
              </button>
            </div>
          }
        }

        @if (b.status !== 'Deactivated') {
          <nav class="links">
            @if (b.orderingEnabled && b.status === 'Active') {
              <a routerLink="orders"
                >Orders <span class="muted">New, preparing, out for delivery</span></a
              >
              <a routerLink="prep"
                >Prep summary <span class="muted">What to make for today's orders</span></a
              >
            }
            @if (b.orderingEnabled) {
              <a routerLink="menu"
                >Menu <span class="muted">Items, prices, today's availability</span></a
              >
            }
            <a routerLink="photos"
              >Photos <span class="muted">{{ b.photos.length }} added</span></a
            >
            <a routerLink="settings"
              >Hours and charges <span class="muted">When you take orders</span></a
            >
            <a routerLink="profile"
              >Business details <span class="muted">Name, description, UPI ID</span></a
            >
            <a [routerLink]="['/customer/b', b.id]"
              >See my page <span class="muted">As customers see it</span></a
            >
          </nav>
        }
      } @else {
        <h1>Sell to your neighbours</h1>
        <p>
          Cook, bake, sell or teach from home? List your business on TowerHaat and residents of your
          society can find you, see your menu and order.
        </p>
        <a class="button primary block" routerLink="register">Start selling</a>
      }
    }
  `,
  styles: `
    .status p {
      margin: 6px 0 0;
    }
    .pending {
      background: #fff7ed;
    }
    .rejected {
      background: #fef2f2;
    }
    .open-switch {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }
    .open-switch p {
      margin: 2px 0 0;
      font-size: 0.875rem;
    }
    .links {
      display: flex;
      flex-direction: column;
      margin-top: 16px;
      border: 1px solid var(--th-border);
      border-radius: var(--th-radius);
      background: var(--th-surface);
    }
    .links a {
      display: flex;
      flex-direction: column;
      padding: 12px var(--th-gap);
      color: var(--th-text);
      font-weight: 600;
      text-decoration: none;
    }
    .links a + a {
      border-top: 1px solid var(--th-border);
    }
    .links .muted {
      font-size: 0.875rem;
      font-weight: 400;
    }
    .block {
      display: block;
      text-align: center;
      text-decoration: none;
    }
  `,
})
export default class OwnerHome implements OnInit {
  protected readonly store = inject(OwnerStore);
  private readonly api = inject(OwnerApi);

  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      await this.store.load(true);
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  protected async toggleOpen(isOpen: boolean): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      this.store.set(await this.api.setOpen(isOpen));
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
