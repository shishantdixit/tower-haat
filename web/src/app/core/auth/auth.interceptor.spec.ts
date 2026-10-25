import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { authResponse } from './testing';

describe('authInterceptor', () => {
  let client: HttpClient;
  let http: HttpTestingController;
  let auth: AuthService;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);

    const verified = auth.verifyOtp('9876543210', '123456');
    http.expectOne('/api/auth/verify').flush(authResponse());
    await verified;
  });

  afterEach(() => http.verify());

  it('sends the access token to the API', () => {
    void firstValueFrom(client.get('/api/me'));

    expect(http.expectOne('/api/me').request.headers.get('Authorization')).toBe('Bearer access-1');
  });

  it('does not send it to auth endpoints or other hosts', () => {
    void firstValueFrom(client.post('/api/auth/logout', null));
    void firstValueFrom(client.get('https://example.com/api/x'));

    expect(http.expectOne('/api/auth/logout').request.headers.has('Authorization')).toBe(false);
    expect(http.expectOne('https://example.com/api/x').request.headers.has('Authorization')).toBe(
      false,
    );
  });

  it('renews an expired token once and retries', async () => {
    const result = firstValueFrom(client.get<{ ok: boolean }>('/api/me'));

    http.expectOne('/api/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/auth/refresh').flush({ ...authResponse(), accessToken: 'access-2' });
    await new Promise((resolve) => setTimeout(resolve));
    const retry = http.expectOne('/api/me');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
    retry.flush({ ok: true });

    expect(await result).toEqual({ ok: true });
  });
});
