import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { AuthService } from './core/auth/auth.service';
import { authResponse } from './core/auth/testing';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('hides the tabs until the resident has signed up', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('.brand')?.textContent).toContain('TowerHaat');
    expect(el.querySelector('nav.tabs')).toBeNull();
  });

  it('shows the flat and the admin tab to a platform admin resident', async () => {
    const verified = TestBed.inject(AuthService).verifyOtp('9876543210', '123456');
    TestBed.inject(HttpTestingController)
      .expectOne('/api/auth/verify')
      .flush(
        authResponse({
          isPlatformAdmin: true,
          profile: {
            societyId: 's',
            societyName: 'Ace Divino',
            flatId: 'f',
            tower: 'A',
            flat: '101',
            isListedFlat: true,
          },
        }),
      );
    await verified;

    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('.me')?.textContent).toContain('A-101');
    const tabs = Array.from(el.querySelectorAll('nav.tabs a')).map((a) => a.textContent?.trim());
    expect(tabs).toEqual(['Shop', 'My business', 'Admin']);
  });
});
