import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { Directory } from '../core/api/models';
import CustomerHome from './customer-home';

const directory: Directory = {
  categories: [
    { id: 'kitchen', name: 'Home kitchen', count: 1 },
    { id: 'bakery', name: 'Bakery and desserts', count: 1 },
  ],
  businesses: [
    {
      id: 'b1',
      name: "Asha's Kitchen",
      description: null,
      businessTypeName: 'Food',
      categoryId: 'kitchen',
      categoryName: 'Home kitchen',
      orderingEnabled: true,
      isOpenNow: true,
      opensAt: '08:00',
      closesAt: '21:00',
      coverPhotoUrl: null,
    },
    {
      id: 'b2',
      name: "Meena's Bakes",
      description: null,
      businessTypeName: 'Food',
      categoryId: 'bakery',
      categoryName: 'Bakery and desserts',
      orderingEnabled: true,
      isOpenNow: false,
      opensAt: '16:00',
      closesAt: '20:30',
      coverPhotoUrl: null,
    },
  ],
};

describe('CustomerHome', () => {
  it('lists businesses with open badges and filters by category', async () => {
    TestBed.configureTestingModule({
      imports: [CustomerHome],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(CustomerHome);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne('/api/directory/businesses').flush(directory);
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const names = () =>
      Array.from(el.querySelectorAll('.business strong')).map((e) => e.textContent?.trim());

    expect(names()).toEqual(["Asha's Kitchen", "Meena's Bakes"]);
    expect(el.querySelector('.badge.open')?.textContent).toContain('till 9 pm');
    expect(el.textContent).toContain('Closed · opens 4 pm');

    (
      Array.from(el.querySelectorAll('.chips button')).find((b) =>
        b.textContent?.includes('Bakery'),
      ) as HTMLButtonElement
    ).click();
    fixture.detectChanges();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(names()).toEqual(["Meena's Bakes"]);
  });
});
