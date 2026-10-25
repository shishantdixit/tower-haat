import { isSettled, paymentLabel } from './payment-status';

describe('payment labels', () => {
  it('reads differently to the customer and the owner', () => {
    expect(paymentLabel('Cod', 'Pending', 'customer')).toBe('Pay in cash on delivery');
    expect(paymentLabel('Cod', 'Pending', 'owner')).toBe('Cash to collect');
    expect(paymentLabel('Upi', 'CustomerMarkedPaid', 'customer')).toContain(
      'waiting for the business',
    );
    expect(paymentLabel('Upi', 'CustomerMarkedPaid', 'owner')).toBe('Customer says paid');
    expect(paymentLabel('Upi', 'Confirmed', 'owner')).toBe('UPI received');
  });

  it('knows when money has been settled', () => {
    expect(isSettled('Confirmed')).toBe(true);
    expect(isSettled('Collected')).toBe(true);
    expect(isSettled('CustomerMarkedPaid')).toBe(false);
  });
});
