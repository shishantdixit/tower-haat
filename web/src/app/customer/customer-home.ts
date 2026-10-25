import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { DirectoryApi } from '../core/api/business.api';
import { errorMessage } from '../core/api/errors';
import { Directory } from '../core/api/models';
import { AuthService } from '../core/auth/auth.service';
import { Time12Pipe } from '../shared/formatting';

/** C-02: the society's businesses by category, open ones first. */
@Component({
  selector: 'app-customer-home',
  imports: [RouterLink, Time12Pipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Namaste, {{ firstName() }}</h1>
    <p class="muted">
      {{ profile()?.societyName }} · Tower {{ profile()?.tower }}, Flat {{ profile()?.flat }}
    </p>

    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }

    @if (directory(); as d) {
      @if (d.businesses.length === 0) {
        <div class="card">
          <p>No businesses here yet. Do you cook, bake, sell or teach from home?</p>
          <a class="button primary" routerLink="/owner">Start selling</a>
        </div>
      } @else {
        <div class="chips" role="tablist" aria-label="Categories">
          <button type="button" role="tab" [class.on]="!category()" (click)="category.set(null)">
            All
          </button>
          @for (chip of d.categories; track chip.id) {
            <button
              type="button"
              role="tab"
              [class.on]="category() === chip.id"
              (click)="category.set(chip.id)"
            >
              {{ chip.name }}
            </button>
          }
        </div>

        <div class="stack">
          @for (b of visible(); track b.id) {
            <a class="card business" [routerLink]="['b', b.id]">
              @if (b.coverPhotoUrl) {
                <img [src]="b.coverPhotoUrl" alt="" loading="lazy" />
              } @else {
                <div class="placeholder" aria-hidden="true">{{ b.name[0] }}</div>
              }
              <div class="info">
                <strong>{{ b.name }}</strong>
                <span class="muted">{{ b.categoryName }}</span>
                @if (b.isOpenNow) {
                  <span class="badge open">
                    {{
                      b.opensAt === b.closesAt
                        ? 'Open all day'
                        : 'Open · till ' + (b.closesAt | time12)
                    }}
                  </span>
                } @else {
                  <span class="badge">Closed · opens {{ b.opensAt | time12 }}</span>
                }
              </div>
            </a>
          }
        </div>
      }
    } @else if (!error()) {
      <p class="muted">Loading…</p>
    }
  `,
  styles: `
    .chips {
      display: flex;
      gap: 8px;
      overflow-x: auto;
      margin: 16px calc(-1 * var(--th-gap));
      padding: 0 var(--th-gap) 4px;
      scrollbar-width: none;
    }
    .chips button {
      flex-shrink: 0;
      min-height: 36px;
      padding: 6px 14px;
      border-radius: 999px;
      font-weight: 500;
    }
    .chips button.on {
      border-color: var(--th-primary);
      background: var(--th-primary);
      color: var(--th-primary-contrast);
    }
    .business {
      display: flex;
      gap: 12px;
      padding: 10px;
      color: inherit;
      text-decoration: none;
    }
    .business img,
    .placeholder {
      width: 88px;
      height: 88px;
      border-radius: 8px;
      object-fit: cover;
      flex-shrink: 0;
    }
    .placeholder {
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--th-border);
      color: var(--th-muted);
      font-size: 2rem;
      font-weight: 700;
    }
    .info {
      display: flex;
      flex-direction: column;
      gap: 2px;
      min-width: 0;
    }
    .info strong {
      font-size: 1.0625rem;
    }
    .info .muted {
      font-size: 0.875rem;
    }
    .badge {
      align-self: flex-start;
      margin-top: 4px;
      padding: 2px 8px;
      border-radius: 999px;
      background: var(--th-border);
      font-size: 0.75rem;
      font-weight: 600;
    }
    .badge.open {
      background: #dcfce7;
      color: var(--th-success);
    }
  `,
})
export default class CustomerHome implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly api = inject(DirectoryApi);

  protected readonly profile = computed(() => this.auth.user()?.profile ?? null);
  protected readonly firstName = computed(() => this.auth.user()?.name?.split(' ')[0] ?? '');
  protected readonly directory = signal<Directory | null>(null);
  protected readonly category = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly visible = computed(() => {
    const businesses = this.directory()?.businesses ?? [];
    const category = this.category();
    return category ? businesses.filter((b) => b.categoryId === category) : businesses;
  });

  async ngOnInit(): Promise<void> {
    try {
      this.directory.set(await this.api.list());
    } catch (error) {
      this.error.set(errorMessage(error));
    }
  }
}
