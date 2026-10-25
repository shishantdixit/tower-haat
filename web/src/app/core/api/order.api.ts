import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  Order,
  OrderSummary,
  OwnerOrders,
  OwnerOrderTab,
  PaymentAction,
  PlaceOrderRequest,
  PrepSummary,
} from './models';

/** The customer's orders (C-06, C-07, C-09). */
@Injectable({ providedIn: 'root' })
export class OrderApi {
  private readonly http = inject(HttpClient);

  place(request: PlaceOrderRequest): Promise<Order> {
    return firstValueFrom(this.http.post<Order>('/api/orders', request));
  }

  list(): Promise<OrderSummary[]> {
    return firstValueFrom(this.http.get<OrderSummary[]>('/api/orders'));
  }

  get(orderId: string): Promise<Order> {
    return firstValueFrom(this.http.get<Order>(`/api/orders/${orderId}`));
  }

  cancel(orderId: string): Promise<Order> {
    return firstValueFrom(this.http.post<Order>(`/api/orders/${orderId}/cancel`, null));
  }

  /** C-08: "I've paid", with the UPI reference if known. */
  markPaid(orderId: string, utr: string | null): Promise<Order> {
    return firstValueFrom(this.http.post<Order>(`/api/orders/${orderId}/paid`, { utr }));
  }
}

/** Orders placed with the owner's business (B-05, B-06, B-07). */
@Injectable({ providedIn: 'root' })
export class OwnerOrderApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/my/business/orders';

  list(tab: OwnerOrderTab): Promise<OwnerOrders> {
    return firstValueFrom(this.http.get<OwnerOrders>(this.base, { params: { tab } }));
  }

  get(orderId: string): Promise<Order> {
    return firstValueFrom(this.http.get<Order>(`${this.base}/${orderId}`));
  }

  accept(orderId: string): Promise<Order> {
    return firstValueFrom(this.http.post<Order>(`${this.base}/${orderId}/accept`, null));
  }

  reject(orderId: string, reason: string): Promise<Order> {
    return firstValueFrom(this.http.post<Order>(`${this.base}/${orderId}/reject`, { reason }));
  }

  advance(orderId: string): Promise<Order> {
    return firstValueFrom(this.http.post<Order>(`${this.base}/${orderId}/advance`, null));
  }

  cancel(orderId: string, reason: string): Promise<Order> {
    return firstValueFrom(this.http.post<Order>(`${this.base}/${orderId}/cancel`, { reason }));
  }

  /** B-08: UPI received, cash collected, disputed or refunded. */
  payment(orderId: string, action: PaymentAction, note: string | null = null): Promise<Order> {
    return firstValueFrom(
      this.http.post<Order>(`${this.base}/${orderId}/payment`, { action, note }),
    );
  }

  prepSummary(): Promise<PrepSummary> {
    return firstValueFrom(this.http.get<PrepSummary>('/api/my/business/prep-summary'));
  }
}
