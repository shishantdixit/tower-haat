import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { authResponse } from './testing';

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('keeps the session after a verified code', async () => {
    const verified = auth.verifyOtp('9876543210', '123456');
    const request = http.expectOne('/api/auth/verify');
    expect(request.request.body).toEqual({ phone: '9876543210', code: '123456' });
    request.flush(authResponse());

    await verified;
    expect(auth.signedIn()).toBe(true);
    expect(auth.accessToken).toBe('access-1');
    expect(auth.hasProfile()).toBe(false);
  });

  it('shares one refresh request between concurrent callers', async () => {
    const first = auth.refresh();
    const second = auth.refresh();
    http.expectOne('/api/auth/refresh').flush(authResponse());

    expect(await first).toBe(true);
    expect(await second).toBe(true);
  });

  it('ends the session when refresh fails', async () => {
    const verified = auth.verifyOtp('9876543210', '123456');
    http.expectOne('/api/auth/verify').flush(authResponse());
    await verified;

    const refreshed = auth.refresh();
    http
      .expectOne('/api/auth/refresh')
      .flush({ detail: 'Please sign in again.' }, { status: 401, statusText: 'Unauthorized' });

    expect(await refreshed).toBe(false);
    expect(auth.signedIn()).toBe(false);
  });

  it('keeps the session when refresh fails for another reason', async () => {
    const verified = auth.verifyOtp('9876543210', '123456');
    http.expectOne('/api/auth/verify').flush(authResponse());
    await verified;

    const refreshed = auth.refresh();
    http
      .expectOne('/api/auth/refresh')
      .flush(null, { status: 429, statusText: 'Too Many Requests' });

    expect(await refreshed).toBe(false);
    expect(auth.signedIn()).toBe(true);
  });

  it('clears the session on logout even if the server call fails', async () => {
    const verified = auth.verifyOtp('9876543210', '123456');
    http.expectOne('/api/auth/verify').flush(authResponse());
    await verified;

    const loggedOut = auth.logout();
    http.expectOne('/api/auth/logout').flush(null, { status: 500, statusText: 'Error' });

    await expect(loggedOut).rejects.toBeTruthy();
    expect(auth.signedIn()).toBe(false);
  });
});
