import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../core/api/errors';
import { ImportFlatsResult, Tower } from '../core/api/models';
import { SocietyAdminApi } from '../core/api/society.api';

/** A-06: the tower and flat list of one society, with bulk upload. */
@Component({
  selector: 'app-admin-society-detail',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/admin">← Societies</a></p>
    <h1>Towers and flats</h1>
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }

    <h2>Bulk upload</h2>
    <form class="stack card" [formGroup]="importForm" (ngSubmit)="import()">
      <p class="muted">
        One <code>Tower,Flat</code> pair per line, for example <code>A,101</code>. Flats already
        listed are skipped, so the same file can be uploaded again.
      </p>
      <input type="file" accept=".csv,.txt,text/csv,text/plain" (change)="readFile($event)" />
      <textarea [formControl]="csv" placeholder="Tower,Flat&#10;A,101&#10;A,102"></textarea>
      <button class="primary" type="submit" [disabled]="busy() || !csv.value.trim()">Upload</button>
      @if (result(); as r) {
        <p class="success" role="status">
          {{ r.towersCreated }} towers and {{ r.flatsCreated }} flats added,
          {{ r.flatsAlreadyListed }} already listed.
        </p>
        @for (e of r.errors; track e.line) {
          <p class="error">Line {{ e.line }}: {{ e.message }}</p>
        }
      }
    </form>

    <h2>{{ towers().length }} towers</h2>
    <form class="row" [formGroup]="towerForm" (ngSubmit)="addTower()">
      <input type="text" maxlength="50" placeholder="New tower" [formControl]="towerName" />
      <button type="submit" [disabled]="busy() || !towerName.value.trim()">Add tower</button>
    </form>

    <div class="stack list">
      @for (tower of towers(); track tower.id) {
        <details class="card">
          <summary>
            <strong>Tower {{ tower.name }}</strong>
            <span class="muted"> · {{ tower.flats.length }} flats</span>
          </summary>
          <div class="flats">
            @for (flat of tower.flats; track flat.id) {
              <span class="flat">
                {{ flat.number }}
                <button
                  type="button"
                  class="link danger"
                  [attr.aria-label]="'Remove flat ' + flat.number"
                  [disabled]="busy()"
                  (click)="deleteFlat(flat.id)"
                >
                  ×
                </button>
              </span>
            }
          </div>
          <form class="row" (submit)="$event.preventDefault(); addFlat(tower.id, flatInput)">
            <input #flatInput type="text" maxlength="20" placeholder="New flat" />
            <button type="submit" [disabled]="busy()">Add flat</button>
            <button type="button" class="danger" [disabled]="busy()" (click)="deleteTower(tower)">
              Remove tower
            </button>
          </form>
        </details>
      }
    </div>
  `,
  styles: `
    .list {
      margin-top: 12px;
    }
    .row > input {
      flex: 1 1 140px;
      width: auto;
    }
    summary {
      cursor: pointer;
      min-height: 32px;
    }
    .flats {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
      margin: 12px 0;
    }
    .flat {
      padding: 4px 4px 4px 10px;
      border: 1px solid var(--th-border);
      border-radius: 999px;
      font-size: 0.875rem;
    }
    .flat button {
      padding: 0 6px;
    }
  `,
})
export default class SocietyDetail implements OnInit {
  private readonly api = inject(SocietyAdminApi);

  readonly societyId = input.required<string>();

  protected readonly towers = signal<Tower[]>([]);
  protected readonly result = signal<ImportFlatsResult | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly csv = new FormControl('', { nonNullable: true });
  protected readonly towerName = new FormControl('', { nonNullable: true });
  // Form groups make (ngSubmit) handle the submit instead of the browser reloading the page.
  protected readonly importForm = new FormGroup({ csv: this.csv });
  protected readonly towerForm = new FormGroup({ name: this.towerName });

  ngOnInit(): Promise<void> {
    return this.run(() => this.reload());
  }

  protected async readFile(event: Event): Promise<void> {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (file) {
      this.csv.setValue(await file.text());
    }
  }

  protected import(): Promise<void> {
    return this.run(async () => {
      this.result.set(await this.api.importFlats(this.societyId(), this.csv.value));
      await this.reload();
    });
  }

  protected addTower(): Promise<void> {
    return this.run(async () => {
      await this.api.addTower(this.societyId(), this.towerName.value);
      this.towerName.reset();
      await this.reload();
    });
  }

  protected addFlat(towerId: string, input: HTMLInputElement): Promise<void> {
    return this.run(async () => {
      await this.api.addFlat(this.societyId(), towerId, input.value);
      input.value = '';
      await this.reload();
    });
  }

  protected deleteFlat(flatId: string): Promise<void> {
    return this.run(async () => {
      await this.api.deleteFlat(this.societyId(), flatId);
      await this.reload();
    });
  }

  protected deleteTower(tower: Tower): Promise<void> {
    if (!confirm(`Remove tower ${tower.name} and its ${tower.flats.length} flats?`)) {
      return Promise.resolve();
    }
    return this.run(async () => {
      await this.api.deleteTower(this.societyId(), tower.id);
      await this.reload();
    });
  }

  private async reload(): Promise<void> {
    this.towers.set(await this.api.towers(this.societyId()));
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
