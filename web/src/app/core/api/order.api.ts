import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  Order,
  OrderSummary,
  OwnerOrders,
  OwnerOrderTab,
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

  prepSummary(): Promise<PrepSummary> {
    return firstValueFrom(this.http.get<PrepSummary>('/api/my/business/prep-summary'));
  }
}
