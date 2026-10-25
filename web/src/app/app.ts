import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="bar">
      <a class="brand" routerLink="/">TowerHaat</a>
    </header>
    <main class="content">
      <router-outlet />
    </main>
    <!-- Temporary area switcher until M1 adds login and roles. -->
    <nav class="tabs" aria-label="Areas">
      <a routerLink="/customer" routerLinkActive="active">Shop</a>
      <a routerLink="/owner" routerLinkActive="active">My business</a>
      <a routerLink="/admin" routerLinkActive="active">Admin</a>
    </nav>
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
      padding: 12px var(--th-gap);
      padding-top: max(12px, env(safe-area-inset-top));
      background: var(--th-primary);
    }
    .brand {
      color: var(--th-primary-contrast);
      font-size: 1.25rem;
      font-weight: 700;
      text-decoration: none;
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
    .tabs a {
      flex: 1;
      padding: 12px 4px;
      color: var(--th-muted);
      text-align: center;
      text-decoration: none;
    }
    .tabs a.active {
      color: var(--th-primary);
      font-weight: 600;
    }
  `,
})
export class App {}
