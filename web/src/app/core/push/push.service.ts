import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { SwPush } from '@angular/service-worker';
import { firstValueFrom, map } from 'rxjs';
import { isIos, isStandalone } from './platform';

/**
 * Web push for this device: new orders for owners, status changes for customers. Needs the
 * service worker (production builds) and, on iPhone, the app added to the home screen.
 */
@Injectable({ providedIn: 'root' })
export class PushService {
  private readonly swPush = inject(SwPush);
  private readonly http = inject(HttpClient);

  /** Push works in this browser right now. */
  readonly supported =
    this.swPush.isEnabled &&
    typeof window !== 'undefined' &&
    'PushManager' in window &&
    'Notification' in window;

  /** iPhone in Safari: push only works after "Add to Home Screen". */
  readonly needsInstall = !this.supported && isIos() && !isStandalone();

  readonly permission = signal<NotificationPermission>(
    typeof Notification === 'undefined' ? 'default' : Notification.permission,
  );
  readonly subscribed = toSignal(this.swPush.subscription.pipe(map((s) => s !== null)), {
    initialValue: false,
  });

  async enable(): Promise<void> {
    const { publicKey } = await firstValueFrom(
      this.http.get<{ publicKey: string }>('/api/push/public-key'),
    );
    try {
      const subscription = await this.swPush.requestSubscription({ serverPublicKey: publicKey });
      await this.save(subscription);
    } finally {
      this.permission.set(Notification.permission);
    }
  }

  /** After signing in, point this browser's existing subscription at the new user. */
  async resync(): Promise<void> {
    if (!this.supported) {
      return;
    }
    const subscription = await firstValueFrom(this.swPush.subscription);
    if (subscription) {
      await this.save(subscription).catch(() => undefined);
    }
  }

  /** On sign-out: stop pushes for this user on this device. Call while still signed in. */
  async forgetDevice(): Promise<void> {
    if (!this.supported) {
      return;
    }
    const subscription = await firstValueFrom(this.swPush.subscription);
    if (!subscription) {
      return;
    }
    await firstValueFrom(
      this.http.post('/api/push/subscriptions/remove', { endpoint: subscription.endpoint }),
    ).catch(() => undefined);
    await this.swPush.unsubscribe().catch(() => undefined);
  }

  private async save(subscription: PushSubscription): Promise<void> {
    const json = subscription.toJSON();
    await firstValueFrom(
      this.http.post('/api/push/subscriptions', {
        endpoint: json.endpoint,
        p256dh: json.keys?.['p256dh'],
        auth: json.keys?.['auth'],
      }),
    );
  }
}
