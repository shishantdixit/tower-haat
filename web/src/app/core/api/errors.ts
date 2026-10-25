import { HttpErrorResponse } from '@angular/common/http';

/** The API's problem details carry a message meant for the user in `detail`. */
export function errorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const detail = (error.error as { detail?: unknown } | null)?.detail;
    if (typeof detail === 'string' && detail.length > 0) {
      return detail;
    }
    if (error.status === 0) {
      return 'No connection. Check your internet and try again.';
    }
    if (error.status === 429) {
      return 'Too many attempts. Please wait a minute and try again.';
    }
  }
  return 'Something went wrong. Please try again.';
}
