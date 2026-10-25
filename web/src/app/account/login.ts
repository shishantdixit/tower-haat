import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { errorMessage } from '../core/api/errors';
import { AuthService } from '../core/auth/auth.service';

/** Phone number, then the 6-digit code (C-01). */
@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  styles: `
    .legal-links {
      margin-top: 16px;
      font-size: 0.875rem;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Society ka apna bazaar</h1>
    <p class="muted">Kitchens, sellers, tuition and services from your society, in one place.</p>

    @if (step() === 'phone') {
      <form class="stack card" [formGroup]="phoneForm" (ngSubmit)="sendCode()">
        <label class="field">
          <span>Mobile number</span>
          <input
            type="tel"
            inputmode="numeric"
            autocomplete="tel-national"
            placeholder="98765 43210"
            [formControl]="phone"
          />
        </label>
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
        <button class="primary block" type="submit" [disabled]="busy() || phone.invalid">
          Get code
        </button>
      </form>
    } @else {
      <form class="stack card" [formGroup]="codeForm" (ngSubmit)="verify()">
        <p>
          Enter the 6-digit code sent to <strong>{{ phone.value }}</strong
          >.
          <button class="link" type="button" (click)="changeNumber()">Change</button>
        </p>
        @if (developmentCode()) {
          <p class="muted">Development mode, no SMS sent. Code: {{ developmentCode() }}</p>
        }
        <label class="field">
          <span>Code</span>
          <input
            type="text"
            inputmode="numeric"
            autocomplete="one-time-code"
            maxlength="6"
            [formControl]="code"
          />
        </label>
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
        <button class="primary block" type="submit" [disabled]="busy() || code.invalid">
          Sign in
        </button>
        <button
          class="link"
          type="button"
          [disabled]="busy() || resendIn() > 0"
          (click)="sendCode()"
        >
          {{ resendIn() > 0 ? 'Resend code in ' + resendIn() + 's' : 'Resend code' }}
        </button>
      </form>
    }

    <p class="muted small legal-links">
      By continuing you agree to the <a routerLink="/terms">terms of use</a> and
      <a routerLink="/privacy">privacy notice</a>.
    </p>
  `,
})
export default class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly returnUrl = input<string>();

  protected readonly phone = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^[+\d\s-]{10,16}$/)],
  });
  protected readonly code = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^\d{6}$/)],
  });
  // Form groups make (ngSubmit) handle the submit instead of the browser reloading the page.
  protected readonly phoneForm = new FormGroup({ phone: this.phone });
  protected readonly codeForm = new FormGroup({ code: this.code });

  protected readonly step = signal<'phone' | 'code'>('phone');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly resendIn = signal(0);
  protected readonly developmentCode = signal<string | null>(null);

  private timer: ReturnType<typeof setInterval> | undefined;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearInterval(this.timer));
  }

  protected async sendCode(): Promise<void> {
    await this.run(async () => {
      const sent = await this.auth.requestOtp(this.phone.value);
      this.developmentCode.set(sent.developmentCode);
      this.code.reset();
      this.step.set('code');
      this.startResendTimer(sent.resendAfterSeconds);
    });
  }

  protected async verify(): Promise<void> {
    await this.run(async () => {
      const user = await this.auth.verifyOtp(this.phone.value, this.code.value);
      const target = !user.profile && !user.isPlatformAdmin ? '/signup' : (this.returnUrl() ?? '/');
      await this.router.navigateByUrl(target);
    });
  }

  protected changeNumber(): void {
    this.step.set('phone');
    this.error.set(null);
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

  private startResendTimer(seconds: number): void {
    clearInterval(this.timer);
    this.resendIn.set(seconds);
    this.timer = setInterval(() => {
      this.resendIn.update((s) => Math.max(0, s - 1));
      if (this.resendIn() === 0) {
        clearInterval(this.timer);
      }
    }, 1000);
  }
}
