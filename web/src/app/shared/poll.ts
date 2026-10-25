import { DestroyRef, inject } from '@angular/core';

/**
 * Re-runs `refresh` every few seconds while the page is visible, until the component is
 * destroyed. A stand-in for live updates until M5 adds SignalR and push.
 */
export function pollWhileVisible(refresh: () => unknown, intervalMs = 15000): void {
  const timer = setInterval(() => {
    if (document.visibilityState === 'visible') {
      refresh();
    }
  }, intervalMs);
  const onVisible = () => document.visibilityState === 'visible' && refresh();
  document.addEventListener('visibilitychange', onVisible);
  inject(DestroyRef).onDestroy(() => {
    clearInterval(timer);
    document.removeEventListener('visibilitychange', onVisible);
  });
}
