import { HttpClient } from '@angular/common/http';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { errorMessage } from '../core/api/errors';

interface BackupFile {
  name: string;
  sizeBytes: number;
  createdAt: string;
}

/** Nightly database backups on the server; download one now and then to keep a copy elsewhere. */
@Component({
  selector: 'app-backups',
  imports: [RouterLink, DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/admin">← Admin</a></p>
    <h1>Backups</h1>
    <p class="muted">
      A copy of the database is made every night and kept for 14 days on the server. Download one
      every week or so and keep it somewhere else, such as Google Drive.
    </p>
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }
    <button type="button" class="primary" [disabled]="busy()" (click)="makeNow()">
      Back up now
    </button>

    <div class="stack list">
      @for (b of backups(); track b.name) {
        <div class="card row item">
          <div>
            <strong>{{ b.createdAt | date: 'd MMM yyyy, h:mm a' : '+0530' }}</strong>
            <span class="muted"> · {{ b.sizeBytes / 1024 | number: '1.0-0' }} KB</span>
          </div>
          <button type="button" [disabled]="busy()" (click)="download(b)">Download</button>
        </div>
      } @empty {
        <p class="muted">No backups yet.</p>
      }
    </div>
  `,
  styles: `
    .list {
      margin-top: 16px;
    }
    .item {
      justify-content: space-between;
    }
  `,
})
export default class Backups implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly backups = signal<BackupFile[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): Promise<void> {
    return this.run(() => this.reload());
  }

  protected makeNow(): Promise<void> {
    return this.run(async () => {
      await firstValueFrom(this.http.post('/api/admin/backups', null));
      await this.reload();
    });
  }

  /** Fetched with the access token (a plain link cannot send it), then saved through a temporary URL. */
  protected download(backup: BackupFile): Promise<void> {
    return this.run(async () => {
      const blob = await firstValueFrom(
        this.http.get(`/api/admin/backups/${backup.name}`, { responseType: 'blob' }),
      );
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = backup.name;
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 10_000);
    });
  }

  private async reload(): Promise<void> {
    this.backups.set(await firstValueFrom(this.http.get<BackupFile[]>('/api/admin/backups')));
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
