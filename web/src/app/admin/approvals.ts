import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { BusinessAdminApi } from '../core/api/business.api';
import { errorMessage } from '../core/api/errors';
import { AdminBusiness, BusinessStatus } from '../core/api/models';

/** A-02: approve, reject or deactivate businesses, with a note the owner sees. */
@Component({
  selector: 'app-approvals',
  imports: [RouterLink, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/admin">← Admin</a></p>
    <h1>Businesses</h1>

    <div class="tabs" role="tablist">
      @for (s of statuses; track s) {
        <button type="button" role="tab" [class.on]="status() === s" (click)="show(s)">
          {{ s }}
        </button>
      }
    </div>

    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }

    @if (businesses(); as list) {
      @if (list.length === 0) {
        <p class="muted">Nothing here.</p>
      }
      <div class="stack">
        @for (b of list; track b.id) {
          <div class="card stack business">
            <div>
              <strong>{{ b.name }}</strong>
              <p class="muted">
                {{ b.businessTypeName }} · {{ b.categoryName }} · {{ b.societyName }}
              </p>
              <p>
                {{ b.ownerName }}, {{ b.ownerAddress }} ·
                <a [href]="'tel:' + b.ownerPhone">{{ b.ownerPhone }}</a>
              </p>
              @if (b.description) {
                <p class="description">{{ b.description }}</p>
              }
              <p class="muted small">
                UPI {{ b.upiId ?? 'not given' }}
                @if (b.fssaiNumber) {
                  · FSSAI {{ b.fssaiNumber }}
                }
                · registered {{ b.createdAt | date: 'd MMM, h:mm a' : '+0530' }}
              </p>
              @if (b.statusNote) {
                <p class="small">Note: {{ b.statusNote }}</p>
              }
            </div>
            @if (b.photoUrls.length) {
              <div class="photos">
                @for (url of b.photoUrls; track url) {
                  <a [href]="url" target="_blank" rel="noopener"
                    ><img [src]="url" alt="" loading="lazy"
                  /></a>
                }
              </div>
            }
            <textarea
              #note
              rows="2"
              maxlength="500"
              placeholder="Note for the owner (needed to reject or deactivate)"
            ></textarea>
            <div class="row">
              @if (b.status !== 'Active') {
                <button
                  type="button"
                  class="primary"
                  [disabled]="busy()"
                  (click)="decide(b, 'approve', note.value)"
                >
                  {{ b.status === 'Deactivated' ? 'Reactivate' : 'Approve' }}
                </button>
              }
              @if (b.status === 'Pending') {
                <button type="button" [disabled]="busy()" (click)="decide(b, 'reject', note.value)">
                  Reject
                </button>
              }
              @if (b.status === 'Active' || b.status === 'Pending') {
                <button
                  type="button"
                  class="danger"
                  [disabled]="busy()"
                  (click)="decide(b, 'deactivate', note.value)"
                >
                  Deactivate
                </button>
              }
            </div>
          </div>
        }
      </div>
    }
  `,
  styles: `
    .tabs {
      display: flex;
      gap: 6px;
      overflow-x: auto;
      margin-bottom: 16px;
    }
    .tabs button {
      flex-shrink: 0;
      min-height: 36px;
      border-radius: 999px;
      font-weight: 500;
    }
    .tabs button.on {
      border-color: var(--th-primary);
      background: var(--th-primary);
      color: var(--th-primary-contrast);
    }
    .business p {
      margin: 4px 0;
    }
    .description {
      white-space: pre-line;
    }
    .small {
      font-size: 0.875rem;
    }
    .photos {
      display: flex;
      gap: 6px;
      overflow-x: auto;
    }
    .photos img {
      width: 96px;
      height: 96px;
      border-radius: 8px;
      object-fit: cover;
    }
    textarea {
      min-height: 64px;
    }
  `,
})
export default class Approvals implements OnInit {
  private readonly api = inject(BusinessAdminApi);

  protected readonly statuses: BusinessStatus[] = ['Pending', 'Active', 'Rejected', 'Deactivated'];
  protected readonly status = signal<BusinessStatus>('Pending');
  protected readonly businesses = signal<AdminBusiness[] | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): Promise<void> {
    return this.show('Pending');
  }

  protected show(status: BusinessStatus): Promise<void> {
    this.status.set(status);
    return this.run(async () => this.businesses.set(await this.api.list(status)));
  }

  protected decide(
    business: AdminBusiness,
    decision: 'approve' | 'reject' | 'deactivate',
    note: string,
  ): Promise<void> {
    return this.run(async () => {
      await this.api.decide(business.id, decision, note.trim() || null);
      this.businesses.set(await this.api.list(this.status()));
    });
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
