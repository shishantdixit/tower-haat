import { rupees, time12 } from './formatting';

describe('formatting', () => {
  it('shows 24-hour times as 12-hour', () => {
    expect(time12('08:00')).toBe('8 am');
    expect(time12('12:30')).toBe('12:30 pm');
    expect(time12('00:15')).toBe('12:15 am');
    expect(time12('21:05')).toBe('9:05 pm');
    expect(time12(null)).toBe('');
  });

  it('shows rupees without paise unless there are some', () => {
    expect(rupees(120)).toBe('₹120');
    expect(rupees(99.5)).toBe('₹99.50');
    expect(rupees(125000)).toBe('₹1,25,000');
  });
});
