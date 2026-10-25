import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { errorMessage } from '../core/api/errors';

/** The newest warnings and errors from the server's log, newest first. */
@Component({
  selector: 'app-error-log',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/admin">← Admin</a></p>
    <div class="row head">
      <h1>Error log</h1>
      <button type="button" [disabled]="busy()" (click)="load()">Refresh</button>
    </div>
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }
    @if (lines(); as list) {
      @if (list.length === 0) {
        <p class="muted">No warnings or errors. 🎉</p>
      } @else {
        <pre class="card">{{
          list.join(
            '
'
          )
        }}</pre>
      }
    }
  `,
  styles: `
    .head {
      justify-content: space-between;
    }
    pre {
      overflow-x: auto;
      font-size: 0.75rem;
      line-height: 1.4;
      white-space: pre-wrap;
      word-break: break-word;
    }
  `,
})
export default class ErrorLog implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly lines = signal<string[] | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  protected async load(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      this.lines.set(
        (await firstValueFrom(this.http.get<{ lines: string[] }>('/api/admin/logs/errors'))).lines,
      );
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
