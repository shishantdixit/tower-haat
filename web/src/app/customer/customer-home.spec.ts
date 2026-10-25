import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import CustomerHome from './customer-home';

describe('CustomerHome', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CustomerHome],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('shows the server status from /api/health', async () => {
    const fixture = TestBed.createComponent(CustomerHome);
    fixture.detectChanges();

    http.expectOne('/api/health').flush({ status: 'ok', utcNow: '2026-01-01T00:00:00Z' });
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('.status')?.textContent).toContain(
      'ok',
    );
  });

  it('shows unreachable when the server is down', async () => {
    const fixture = TestBed.createComponent(CustomerHome);
    fixture.detectChanges();

    http.expectOne('/api/health').flush('', { status: 503, statusText: 'Unavailable' });
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('.status')?.textContent).toContain(
      'unreachable',
    );
  });
});
