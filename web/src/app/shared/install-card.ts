import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { InstallService } from '../core/push/install.service';
import { isIos, isStandalone } from '../core/push/platform';

const DISMISSED_KEY = 'th.install-dismissed';

/** Suggests installing TowerHaat on the home screen: one tap on Android, steps on iPhone. */
@Component({
  selector: 'app-install-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (visible()) {
      <div class="card install">
        <div>
          <strong>Add TowerHaat to your home screen</strong>
          @if (ios) {
            <p class="muted">
              In Safari, tap Share <span aria-hidden="true">⎙</span> then
              <strong>Add to Home Screen</strong>.
            </p>
          } @else {
            <p class="muted">Opens like an app, and order alerts reach you.</p>
          }
        </div>
        <div class="row">
          @if (!ios) {
            <button type="button" class="primary" (click)="install.install()">Install</button>
          }
          <button type="button" class="link" (click)="dismiss()">Not now</button>
        </div>
      </div>
    }
  `,
  styles: `
    .install {
      display: flex;
      flex-direction: column;
      gap: 8px;
      margin: 16px 0;
    }
    .install p {
      margin: 4px 0 0;
      font-size: 0.875rem;
    }
  `,
})
export class InstallCard {
  protected readonly install = inject(InstallService);
  protected readonly ios = isIos();
  private readonly dismissed = signal(readDismissed());

  protected readonly visible = computed(
    () => !this.dismissed() && !isStandalone() && (this.ios || this.install.canInstall()),
  );

  protected dismiss(): void {
    this.dismissed.set(true);
    try {
      localStorage.setItem(DISMISSED_KEY, '1');
    } catch {
      // Without storage it just shows again next time.
    }
  }
}

function readDismissed(): boolean {
  try {
    return localStorage.getItem(DISMISSED_KEY) === '1';
  } catch {
    return false;
  }
}
