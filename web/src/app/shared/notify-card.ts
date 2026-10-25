import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { errorMessage } from '../core/api/errors';
import { PushService } from '../core/push/push.service';

/**
 * Asks for push permission where it matters (new orders for owners, order updates for customers),
 * or explains "Add to Home Screen" on iPhone, where push only works in the installed app.
 */
@Component({
  selector: 'app-notify-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (push.needsInstall) {
      <div class="card notify">
        <strong>Get {{ what() }} on your iPhone</strong>
        <ol>
          <li>Tap the Share button <span aria-hidden="true">⎙</span> in Safari</li>
          <li>Choose <strong>Add to Home Screen</strong></li>
          <li>Open TowerHaat from your home screen and turn on notifications</li>
        </ol>
      </div>
    } @else if (show()) {
      <div class="card notify">
        <strong>Turn on notifications</strong>
        <p class="muted">Get {{ what() }} even when TowerHaat is closed.</p>
        @if (error()) {
          <p class="error">{{ error() }}</p>
        }
        <button type="button" class="primary" [disabled]="busy()" (click)="enable()">
          Turn on
        </button>
      </div>
    } @else if (push.supported && push.permission() === 'denied') {
      <p class="muted small">
        Notifications are blocked for TowerHaat. Allow them in your browser's site settings to get
        {{ what() }}.
      </p>
    }
  `,
  styles: `
    .notify {
      margin: 16px 0;
      border-color: var(--th-primary);
      background: #fff7ed;
    }
    .notify p {
      margin: 4px 0 12px;
    }
    ol {
      margin: 8px 0 0;
      padding-left: 20px;
    }
    .small {
      font-size: 0.875rem;
    }
  `,
})
export class NotifyCard {
  protected readonly push = inject(PushService);

  /** What the person will be told about, e.g. "new order alerts". */
  readonly what = input('updates');

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly show = computed(
    () => this.push.supported && !this.push.subscribed() && this.push.permission() !== 'denied',
  );

  protected async enable(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.push.enable();
    } catch (error) {
      // Browser errors ("Registration failed …") mean nothing to people; API errors carry a readable detail.
      if (this.push.permission() !== 'denied') {
        this.error.set(
          error instanceof Error
            ? 'Notifications could not be turned on in this browser. Try Chrome, or add TowerHaat to your home screen.'
            : errorMessage(error),
        );
      }
    } finally {
      this.busy.set(false);
    }
  }
}
