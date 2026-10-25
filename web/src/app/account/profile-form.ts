import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { errorMessage } from '../core/api/errors';
import { SaveProfileRequest, Society, Tower } from '../core/api/models';
import { SocietyApi } from '../core/api/society.api';
import { AuthService } from '../core/auth/auth.service';
import { PushService } from '../core/push/push.service';

/**
 * Sign-up and profile editing: name, then tower and flat picked from the society's
 * list, typed only when missing from it (C-01). Consent is asked once, at sign-up.
 */
@Component({
  selector: 'app-profile-form',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>{{ mode() === 'signup' ? 'Welcome to TowerHaat' : 'My profile' }}</h1>
    @if (mode() === 'signup') {
      <p class="muted">Tell us who you are and where to deliver.</p>
    }

    <form class="stack card" [formGroup]="form" (ngSubmit)="save()">
      <label class="field">
        <span>Your name</span>
        <input type="text" autocomplete="name" maxlength="80" formControlName="name" />
      </label>

      @if (societies().length > 1 || isEdit()) {
        <label class="field">
          <span>Society</span>
          <select formControlName="societyId">
            @for (society of societies(); track society.id) {
              <option [value]="society.id">{{ society.name }}, {{ society.city }}</option>
            }
          </select>
        </label>
      } @else if (societies().length === 1) {
        <p>
          <strong>{{ societies()[0].name }}</strong
          >, {{ societies()[0].city }}
        </p>
      }

      @if (!manual()) {
        <div class="row pair">
          <label class="field">
            <span>Tower</span>
            <select formControlName="towerId">
              <option value="" disabled>Choose</option>
              @for (tower of towers(); track tower.id) {
                <option [value]="tower.id">{{ tower.name }}</option>
              }
            </select>
          </label>
          <label class="field">
            <span>Flat</span>
            <select formControlName="flatId">
              <option value="" disabled>Choose</option>
              @for (flat of flats(); track flat.id) {
                <option [value]="flat.id">{{ flat.number }}</option>
              }
            </select>
          </label>
        </div>
      } @else {
        @if (towers().length === 0) {
          <p class="muted">The flat list for this society is not ready yet. Please type yours.</p>
        }
        <div class="row pair">
          <label class="field">
            <span>Tower</span>
            <input type="text" maxlength="50" formControlName="manualTower" />
          </label>
          <label class="field">
            <span>Flat</span>
            <input type="text" maxlength="20" formControlName="manualFlat" />
          </label>
        </div>
      }

      @if (towers().length > 0) {
        <label class="check">
          <input type="checkbox" formControlName="manual" />
          <span>My flat is not in the list</span>
        </label>
      }

      @if (!isEdit()) {
        <label class="check">
          <input type="checkbox" formControlName="consent" />
          <span>
            I agree that TowerHaat may use my name, phone number, tower and flat to deliver my
            orders, and share them only with businesses I order from. See the
            <a routerLink="/privacy" target="_blank">privacy notice</a>.
          </span>
        </label>
      }

      @if (error()) {
        <p class="error" role="alert">{{ error() }}</p>
      }
      @if (saved()) {
        <p class="success" role="status">Saved.</p>
      }

      <button class="primary block" type="submit" [disabled]="busy()">
        {{ isEdit() ? 'Save' : 'Continue' }}
      </button>
    </form>

    @if (isEdit()) {
      <div class="card stack danger-zone">
        <h2>Delete my account</h2>
        <p class="muted">
          Removes your name, phone and flat from TowerHaat and signs you out. Past orders keep the
          details written on them for the businesses' records. This cannot be undone.
        </p>
        @if (deleteError()) {
          <p class="error" role="alert">{{ deleteError() }}</p>
        }
        <button type="button" class="danger" [disabled]="busy()" (click)="deleteAccount()">
          Delete my account
        </button>
      </div>
      <p class="muted small">
        <a routerLink="/privacy">Privacy notice</a> · <a routerLink="/terms">Terms of use</a>
      </p>
    }
  `,
  styles: `
    .danger-zone {
      margin-top: 24px;
    }
    .danger-zone h2,
    .danger-zone p {
      margin: 0;
    }
    .small {
      font-size: 0.875rem;
    }
    .pair > .field {
      flex: 1 1 120px;
    }
  `,
})
export default class ProfileForm implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly api = inject(SocietyApi);
  private readonly router = inject(Router);

  readonly mode = input<'signup' | 'edit'>('signup');
  protected readonly isEdit = computed(() => this.mode() === 'edit');

  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true }),
    societyId: new FormControl('', { nonNullable: true }),
    towerId: new FormControl('', { nonNullable: true }),
    flatId: new FormControl('', { nonNullable: true }),
    manual: new FormControl(false, { nonNullable: true }),
    manualTower: new FormControl('', { nonNullable: true }),
    manualFlat: new FormControl('', { nonNullable: true }),
    consent: new FormControl(false, { nonNullable: true, validators: Validators.requiredTrue }),
  });

  protected readonly societies = signal<Society[]>([]);
  protected readonly towers = signal<Tower[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly saved = signal(false);
  protected readonly deleteError = signal<string | null>(null);
  private readonly push = inject(PushService);

  private readonly towerId = toSignal(this.form.controls.towerId.valueChanges, {
    initialValue: '',
  });
  private readonly manualValue = toSignal(this.form.controls.manual.valueChanges, {
    initialValue: false,
  });

  protected readonly manual = computed(() => this.manualValue() || this.towers().length === 0);
  protected readonly flats = computed(
    () => this.towers().find((t) => t.id === this.towerId())?.flats ?? [],
  );

  constructor() {
    this.form.controls.towerId.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.form.controls.flatId.setValue(''));
    this.form.controls.societyId.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((id) => void this.loadTowers(id));
  }

  async ngOnInit(): Promise<void> {
    // Before any await, so text typed while the lists load is never overwritten.
    this.form.controls.name.setValue(this.auth.user()?.name ?? '');
    try {
      const societies = await this.api.listActive();
      this.societies.set(societies);

      const profile = this.auth.user()?.profile;
      const societyId = profile?.societyId ?? societies[0]?.id ?? '';
      this.form.controls.societyId.setValue(societyId, { emitEvent: false });
      if (this.isEdit()) {
        this.form.controls.societyId.disable({ emitEvent: false });
      }
      await this.loadTowers(societyId);

      if (profile?.flatId) {
        const tower = this.towers().find((t) => t.flats.some((f) => f.id === profile.flatId));
        this.form.controls.towerId.setValue(tower?.id ?? '');
        this.form.controls.flatId.setValue(profile.flatId);
      } else if (profile) {
        this.form.controls.manual.setValue(true);
        this.form.controls.manualTower.setValue(profile.tower);
        this.form.controls.manualFlat.setValue(profile.flat);
      }
    } catch (error) {
      this.error.set(errorMessage(error));
    }
  }

  protected async save(): Promise<void> {
    const value = this.form.getRawValue();
    const manual = this.manual();
    const request: SaveProfileRequest = {
      name: value.name.trim(),
      societyId: value.societyId,
      flatId: manual ? null : value.flatId || null,
      manualTower: manual ? value.manualTower.trim() : null,
      manualFlat: manual ? value.manualFlat.trim() : null,
      consent: value.consent,
    };

    const problem = validate(request, this.isEdit());
    if (problem) {
      this.error.set(problem);
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    this.saved.set(false);
    try {
      await this.auth.saveProfile(request);
      if (this.isEdit()) {
        this.saved.set(true);
      } else {
        await this.router.navigateByUrl('/customer');
      }
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }

  protected async deleteAccount(): Promise<void> {
    if (!confirm('Delete your TowerHaat account? This cannot be undone.')) {
      return;
    }
    this.busy.set(true);
    this.deleteError.set(null);
    try {
      await this.push.forgetDevice();
      await this.auth.deleteAccount();
      await this.router.navigateByUrl('/login');
    } catch (error) {
      this.deleteError.set(errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }

  private async loadTowers(societyId: string): Promise<void> {
    this.towers.set(societyId ? await this.api.towers(societyId) : []);
    this.form.controls.towerId.setValue('');
  }
}

/** Mirrors the API's checks so mistakes show without a round trip. */
export function validate(request: SaveProfileRequest, isEdit: boolean): string | null {
  if (request.name.length < 2) {
    return 'Enter your name.';
  }
  if (!request.societyId) {
    return 'Choose your society.';
  }
  if (!request.flatId && !(request.manualTower && request.manualFlat)) {
    return 'Pick your tower and flat.';
  }
  if (!isEdit && !request.consent) {
    return 'Please agree to share your details with businesses you order from.';
  }
  return null;
}
