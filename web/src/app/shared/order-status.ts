import { Pipe, PipeTransform } from '@angular/core';
import { OrderStatus } from '../core/api/models';

const customerLabels: Record<OrderStatus, string> = {
  Placed: 'Waiting for the business to accept',
  Accepted: 'Accepted',
  Preparing: 'Being prepared',
  OutForDelivery: 'On the way',
  Delivered: 'Delivered',
  Rejected: 'Rejected',
  Expired: 'Not accepted in time',
  CancelledByCustomer: 'Cancelled by you',
  CancelledByOwner: 'Cancelled by the business',
};

const ownerLabels: Record<OrderStatus, string> = {
  Placed: 'New',
  Accepted: 'Accepted',
  Preparing: 'Preparing',
  OutForDelivery: 'Out for delivery',
  Delivered: 'Delivered',
  Rejected: 'Rejected',
  Expired: 'Expired',
  CancelledByCustomer: 'Cancelled by customer',
  CancelledByOwner: 'Cancelled by you',
};

export const OPEN_STATUSES: OrderStatus[] = ['Placed', 'Accepted', 'Preparing', 'OutForDelivery'];

export function isOpen(status: OrderStatus): boolean {
  return OPEN_STATUSES.includes(status);
}

@Pipe({ name: 'orderStatus' })
export class OrderStatusPipe implements PipeTransform {
  transform(status: OrderStatus, view: 'customer' | 'owner' = 'customer'): string {
    return (view === 'owner' ? ownerLabels : customerLabels)[status];
  }
}

/** The owner's next step for an open order, or null when there is none. */
export function nextStepLabel(status: OrderStatus): string | null {
  switch (status) {
    case 'Accepted':
      return 'Start preparing';
    case 'Preparing':
      return 'Out for delivery';
    case 'OutForDelivery':
      return 'Mark delivered';
    default:
      return null;
  }
}

/** Minutes left before an unaccepted order expires. */
export function minutesLeft(expiresAt: string | null, now = Date.now()): number | null {
  return expiresAt ? Math.max(0, Math.ceil((Date.parse(expiresAt) - now) / 60000)) : null;
}
