import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { Subject } from 'rxjs';
import { App } from './app';
import { AuthService } from './core/auth/auth.service';
import { PushService } from './core/push/push.service';
import { OrderChanged, RealtimeService } from './core/realtime/realtime.service';
import { authResponse } from './core/auth/testing';

describe('App', () => {
  const orderChanged = new Subject<OrderChanged>();

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RealtimeService, useValue: { orderChanged } },
        {
          provide: PushService,
          useValue: { resync: async () => undefined, forgetDevice: async () => undefined },
        },
      ],
    }).compileComponents();
  });

  it('shows a banner when a new order arrives live', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    orderChanged.next({
      orderId: 'o1',
      businessId: 'b1',
      status: 'Placed',
      paymentStatus: 'Pending',
      isNewOrder: true,
    });
    fixture.detectChanges();
    await fixture.whenStable();

    const banner = (fixture.nativeElement as HTMLElement).querySelector('.new-order');
    expect(banner?.textContent).toContain('New order!');
    expect(banner?.getAttribute('href')).toBe('/owner/orders/o1');
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
    expect(tabs).toEqual(['Shop', 'Orders', 'My business', 'Admin']);
  });
});
