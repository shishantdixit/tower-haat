import { TestBed } from '@angular/core/testing';
import { CartService } from './cart.service';

const asha = { id: 'b1', name: "Asha's Kitchen" };
const ravi = { id: 'b2', name: "Ravi's Bakes" };
const thali = { itemId: 'i1', name: 'Veg thali', price: 120, isVeg: true };
const roti = { itemId: 'i2', name: 'Roti', price: 10, isVeg: true };

describe('CartService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
  });

  it('adds items and keeps totals', () => {
    const cart = TestBed.inject(CartService);

    cart.add(asha, thali);
    cart.add(asha, thali);
    cart.add(asha, roti);

    expect(cart.quantity('i1')).toBe(2);
    expect(cart.count()).toBe(3);
    expect(cart.subtotal()).toBe(250);
  });

  it('holds items from one business only', () => {
    const cart = TestBed.inject(CartService);
    cart.add(asha, thali);

    expect(cart.belongsToAnother('b2')).toBe(true);
    cart.add(ravi, roti);

    expect(cart.cart()?.businessId).toBe('b2');
    expect(cart.count()).toBe(1);
  });

  it('removes a line at zero and empties when nothing is left', () => {
    const cart = TestBed.inject(CartService);
    cart.add(asha, thali);

    cart.setQuantity('i1', 0);

    expect(cart.cart()).toBeNull();
  });

  it('renews the checkout key whenever the cart changes', () => {
    const cart = TestBed.inject(CartService);
    cart.add(asha, thali);
    const first = cart.cart()!.checkoutKey;

    cart.setQuantity('i1', 3);

    expect(cart.cart()!.checkoutKey).not.toBe(first);
  });

  it('survives closing the app', () => {
    TestBed.inject(CartService).add(asha, thali);

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});

    expect(TestBed.inject(CartService).quantity('i1')).toBe(1);
  });
});
