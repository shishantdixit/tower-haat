import { Pipe, PipeTransform } from '@angular/core';

/** "18:30" → "6:30 pm" */
export function time12(value: string | null | undefined): string {
  if (!value) {
    return '';
  }
  const [h, m] = value.split(':').map(Number);
  const suffix = h < 12 ? 'am' : 'pm';
  const hour = h % 12 === 0 ? 12 : h % 12;
  return m === 0 ? `${hour} ${suffix}` : `${hour}:${String(m).padStart(2, '0')} ${suffix}`;
}

@Pipe({ name: 'time12' })
export class Time12Pipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return time12(value);
  }
}

/** ₹120, ₹99.50 */
export function rupees(value: number | null | undefined): string {
  if (value == null) {
    return '';
  }
  return (
    '₹' +
    value.toLocaleString('en-IN', {
      maximumFractionDigits: 2,
      minimumFractionDigits: value % 1 ? 2 : 0,
    })
  );
}

@Pipe({ name: 'rupees' })
export class RupeesPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return rupees(value);
  }
}
