import { isOpen, minutesLeft, nextStepLabel } from './order-status';

describe('order status helpers', () => {
  it('names the owner’s next step', () => {
    expect(nextStepLabel('Placed')).toBeNull();
    expect(nextStepLabel('Accepted')).toBe('Start preparing');
    expect(nextStepLabel('Preparing')).toBe('Out for delivery');
    expect(nextStepLabel('OutForDelivery')).toBe('Mark delivered');
    expect(nextStepLabel('Delivered')).toBeNull();
  });

  it('knows which orders are still open', () => {
    expect(isOpen('Preparing')).toBe(true);
    expect(isOpen('Expired')).toBe(false);
  });

  it('counts minutes left before expiry, never below zero', () => {
    const now = Date.parse('2026-10-25T06:00:00Z');
    expect(minutesLeft('2026-10-25T06:29:10Z', now)).toBe(30);
    expect(minutesLeft('2026-10-25T05:00:00Z', now)).toBe(0);
    expect(minutesLeft(null, now)).toBeNull();
  });
});
