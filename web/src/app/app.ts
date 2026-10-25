import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
import { InstallService } from './core/push/install.service';
import { PushService } from './core/push/push.service';
import { playChime } from './core/realtime/chime';
import { RealtimeService } from './core/realtime/realtime.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="bar">
      <a class="brand" routerLink="/">TowerHaat</a>
      @if (auth.hasProfile()) {
        <a class="me" routerLink="/profile" aria-label="My profile">
          {{ auth.user()?.profile?.tower }}-{{ auth.user()?.profile?.flat }}
        </a>
      }
    </header>
    @if (newOrder(); as orderId) {
      <a
        class="new-order"
        role="alert"
        [routerLink]="['/owner/orders', orderId]"
        (click)="newOrder.set(null)"
      >
        <strong>New order!</strong> Tap to view and accept
      </a>
    }
    <main class="content">
      <router-outlet />
    </main>
    @if (auth.hasProfile() || auth.isPlatformAdmin()) {
      <nav class="tabs" aria-label="Areas">
        @if (auth.hasProfile()) {
          <a
            routerLink="/customer"
            routerLinkActive="active"
            [routerLinkActiveOptions]="{ exact: true }"
            >Shop</a
          >
          <a routerLink="/customer/orders" routerLinkActive="active">Orders</a>
          <a routerLink="/owner" routerLinkActive="active">My business</a>
        }
        @if (auth.isPlatformAdmin()) {
          <a routerLink="/admin" routerLinkActive="active">Admin</a>
        }
        <button type="button" class="tab-button" (click)="logout()">Sign out</button>
      </nav>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      min-height: 100dvh;
    }
    .bar {
      position: sticky;
      top: 0;
      z-index: 1;
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 12px var(--th-gap);
      padding-top: max(12px, env(safe-area-inset-top));
      background: var(--th-primary);
    }
    .brand,
    .me {
      color: var(--th-primary-contrast);
      text-decoration: none;
    }
    .brand {
      font-size: 1.25rem;
      font-weight: 700;
    }
    .me {
      font-size: 0.875rem;
      font-weight: 600;
      opacity: 0.9;
    }
    .new-order {
      position: sticky;
      top: 56px;
      z-index: 2;
      display: block;
      padding: 14px var(--th-gap);
      background: var(--th-success);
      color: #fff;
      text-decoration: none;
      text-align: center;
      animation: pulse 1s ease-in-out 3;
    }
    @keyframes pulse {
      50% {
        opacity: 0.75;
      }
    }
    .content {
      flex: 1;
      width: 100%;
      max-width: 720px;
      margin: 0 auto;
      padding: var(--th-gap);
    }
    .tabs {
      position: sticky;
      bottom: 0;
      display: flex;
      border-top: 1px solid var(--th-border);
      background: var(--th-surface);
      padding-bottom: env(safe-area-inset-bottom);
    }
    .tabs a,
    .tab-button {
      flex: 1;
      min-height: 48px;
      padding: 12px 4px;
      border: 0;
      border-radius: 0;
      background: none;
      color: var(--th-muted);
      font-size: 0.875rem;
      white-space: nowrap;
      font-weight: 500;
      text-align: center;
      text-decoration: none;
    }
    .tabs a.active {
      color: var(--th-primary);
      font-weight: 600;
    }
  `,
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly push = inject(PushService);

  /** The latest new order for an owner, shown as a banner with a chime (B-05). */
  protected readonly newOrder = signal<string | null>(null);
  private bannerTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    inject(InstallService);
    inject(RealtimeService)
      .orderChanged.pipe(takeUntilDestroyed())
      .subscribe((event) => {
        if (event.isNewOrder) {
          playChime();
          this.newOrder.set(event.orderId);
          clearTimeout(this.bannerTimer);
          this.bannerTimer = setTimeout(() => this.newOrder.set(null), 60_000);
        }
      });

    // A browser that was already subscribed now belongs to whoever signed in.
    effect(() => {
      if (this.auth.signedIn()) {
        untracked(() => void this.push.resync());
      }
    });
  }

  protected async logout(): Promise<void> {
    await this.push.forgetDevice();
    await this.auth.logout();
    await this.router.navigate(['/login']);
  }
}
