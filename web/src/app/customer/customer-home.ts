import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of } from 'rxjs';
import { HealthService } from '../core/health.service';

@Component({
  selector: 'app-customer-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Society ka apna bazaar</h1>
    <p>Kitchens, sellers, tuition and services from your society, in one place.</p>
    <p class="status" [class.down]="apiStatus() === 'unreachable'">Server: {{ apiStatus() }}</p>
  `,
  styles: `
    h1 {
      margin: 0 0 8px;
      font-size: 1.5rem;
    }
    .status {
      color: var(--th-muted);
      font-size: 0.875rem;
    }
    .status.down {
      color: #b91c1c;
    }
  `,
})
export default class CustomerHome {
  private readonly health = inject(HealthService);

  protected readonly apiStatus = toSignal(
    this.health.check().pipe(
      map((r) => r.status),
      catchError(() => of('unreachable')),
    ),
    { initialValue: 'checking…' },
  );
}
