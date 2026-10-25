import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { UpiPay } from './upi-pay';

describe('UpiPay', () => {
  it('shows the UPI details and sends "I\'ve paid" with the UTR', async () => {
    TestBed.configureTestingModule({
      imports: [UpiPay],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(UpiPay);
    fixture.componentRef.setInput('orderId', 'o1');
    fixture.componentRef.setInput('upi', {
      upiId: 'asha@okaxis',
      payeeName: "Asha's Kitchen",
      amount: 260,
      link: 'upi://pay?pa=asha%40okaxis&am=260.00',
    });
    let paid = false;
    fixture.componentInstance.paid.subscribe(() => (paid = true));
    fixture.detectChanges();
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('a.primary')?.getAttribute('href')).toBe(
      'upi://pay?pa=asha%40okaxis&am=260.00',
    );
    expect(el.textContent).toContain('₹260');
    expect(el.textContent).toContain('asha@okaxis');

    const utr = el.querySelector('input') as HTMLInputElement;
    utr.value = ' 412345678901 ';
    utr.dispatchEvent(new Event('input'));
    (el.querySelector('form button[type=submit]') as HTMLButtonElement).click();

    const request = TestBed.inject(HttpTestingController).expectOne('/api/orders/o1/paid');
    expect(request.request.body).toEqual({ utr: '412345678901' });
    request.flush({});
    await new Promise((resolve) => setTimeout(resolve));
    expect(paid).toBe(true);
  });
});
