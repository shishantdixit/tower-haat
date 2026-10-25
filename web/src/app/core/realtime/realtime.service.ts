import { Injectable, effect, inject } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from '../auth/auth.service';

export interface OrderChanged {
  orderId: string;
  businessId: string;
  status: string;
  paymentStatus: string;
  isNewOrder: boolean;
}

/**
 * Live order updates while the app is open (B-05, C-07). Connected whenever someone is signed in;
 * screens listen to `orderChanged` and reload the order through the normal API.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly auth = inject(AuthService);
  private connection: HubConnection | null = null;

  readonly orderChanged = new Subject<OrderChanged>();

  constructor() {
    effect(() => {
      if (this.auth.signedIn()) {
        void this.connect();
      } else {
        void this.disconnect();
      }
    });
  }

  private async connect(): Promise<void> {
    if (this.connection) {
      return;
    }
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/orders', {
        accessTokenFactory: async () => (await this.auth.freshAccessToken()) ?? '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();
    connection.on('orderChanged', (event: OrderChanged) => this.orderChanged.next(event));
    this.connection = connection;
    try {
      await connection.start();
    } catch {
      // Offline or the server is restarting; screens still refresh on their slow timer.
    }
  }

  private async disconnect(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    if (connection && connection.state !== HubConnectionState.Disconnected) {
      await connection.stop();
    }
  }
}
