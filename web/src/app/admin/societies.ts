import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../core/api/errors';
import { AdminSociety } from '../core/api/models';
import { SocietyAdminApi } from '../core/api/society.api';

/** A-01: create societies and switch them on or off. */
@Component({
  selector: 'app-admin-societies',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Societies</h1>
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }

    <div class="stack">
      @for (society of societies(); track society.id) {
        <div class="card society">
          <div>
            <a [routerLink]="['societies', society.id]"
              ><strong>{{ society.name }}</strong></a
            >, {{ society.city }}
            @if (!society.isActive) {
              <span class="badge">Inactive</span>
            }
            <p class="muted">
              {{ society.towers }} towers · {{ society.flats }} flats ·
              {{ society.residents }} residents
            </p>
          </div>
          <button type="button" [disabled]="busy()" (click)="toggle(society)">
            {{ society.isActive ? 'Deactivate' : 'Activate' }}
          </button>
        </div>
      }
    </div>

    <h2>Add a society</h2>
    <form class="stack card" [formGroup]="form" (ngSubmit)="create()">
      <label class="field">
        <span>Name</span>
        <input type="text" maxlength="200" formControlName="name" />
      </label>
      <label class="field">
        <span>City</span>
        <input type="text" maxlength="100" formControlName="city" />
      </label>
      <button class="primary" type="submit" [disabled]="busy()">Add society</button>
    </form>
  `,
  styles: `
    .society {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }
    .society p {
      margin: 4px 0 0;
      font-size: 0.875rem;
    }
    .badge {
      margin-left: 6px;
      padding: 2px 8px;
      border-radius: 999px;
      background: var(--th-border);
      font-size: 0.75rem;
    }
  `,
})
export default class Societies implements OnInit {
  private readonly api = inject(SocietyAdminApi);

  protected readonly societies = signal<AdminSociety[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true }),
    city: new FormControl('Noida', { nonNullable: true }),
  });

  ngOnInit(): Promise<void> {
    return this.run(async () => this.societies.set(await this.api.list()));
  }

  protected create(): Promise<void> {
    const { name, city } = this.form.getRawValue();
    return this.run(async () => {
      await this.api.create(name, city);
      this.form.reset();
      this.societies.set(await this.api.list());
    });
  }

  protected toggle(society: AdminSociety): Promise<void> {
    return this.run(async () => {
      await this.api.setActive(society.id, !society.isActive);
      this.societies.set(await this.api.list());
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
