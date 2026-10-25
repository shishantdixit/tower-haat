import { Pipe, PipeTransform } from '@angular/core';
import { PaymentMethod, PaymentStatus } from '../core/api/models';

/** How a payment reads to the customer and to the owner. */
export function paymentLabel(
  method: PaymentMethod,
  status: PaymentStatus,
  view: 'customer' | 'owner',
): string {
  const customer = view === 'customer';
  switch (status) {
    case 'Pending':
      return method === 'Cod'
        ? customer
          ? 'Pay in cash on delivery'
          : 'Cash to collect'
        : customer
          ? 'Not paid yet'
          : 'UPI not paid yet';
    case 'CustomerMarkedPaid':
      return customer ? 'Paid · waiting for the business to confirm' : 'Customer says paid';
    case 'Confirmed':
      return customer ? 'Paid' : 'UPI received';
    case 'Collected':
      return customer ? 'Paid in cash' : 'Cash collected';
    case 'Disputed':
      return customer ? 'The business has not received this payment' : 'Not received';
    case 'Refunded':
      return 'Refunded';
  }
}

@Pipe({ name: 'paymentLabel' })
export class PaymentLabelPipe implements PipeTransform {
  transform(
    status: PaymentStatus,
    method: PaymentMethod,
    view: 'customer' | 'owner' = 'customer',
  ): string {
    return paymentLabel(method, status, view);
  }
}

export function isSettled(status: PaymentStatus): boolean {
  return status === 'Confirmed' || status === 'Collected';
}
