import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { AuthService } from './auth.service';
import { authResponse } from './testing';
import { platformAdminGuard, profileGuard, signedInGuard } from './guards';

describe('guards', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  async function signIn(user: Parameters<typeof authResponse>[0] = {}): Promise<void> {
    const verified = TestBed.inject(AuthService).verifyOtp('9876543210', '123456');
    http.expectOne('/api/auth/verify').flush(authResponse(user));
    await verified;
  }

  function run(guard: CanActivateFn, url = '/customer'): string | boolean {
    const result = TestBed.runInInjectionContext(() =>
      guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
    );
    return result instanceof UrlTree
      ? TestBed.inject(Router).serializeUrl(result)
      : (result as boolean);
  }

  it('sends signed-out users to login and back afterwards', () => {
    expect(run(signedInGuard, '/customer')).toBe('/login?returnUrl=%2Fcustomer');
  });

  it('sends users without a profile to sign-up', async () => {
    await signIn();
    expect(run(signedInGuard)).toBe(true);
    expect(run(profileGuard)).toBe('/signup');
  });

  it('lets residents with a profile in', async () => {
    await signIn({
      profile: {
        societyId: 's',
        societyName: 'Ace Divino',
        flatId: 'f',
        tower: 'A',
        flat: '101',
        isListedFlat: true,
      },
    });
    expect(run(profileGuard)).toBe(true);
    expect(run(platformAdminGuard)).toBe('/');
  });

  it('lets platform admins into admin', async () => {
    await signIn({ isPlatformAdmin: true });
    expect(run(platformAdminGuard)).toBe(true);
  });
});
