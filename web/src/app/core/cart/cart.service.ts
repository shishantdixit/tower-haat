import { Injectable, computed, signal } from '@angular/core';

export interface CartLine {
  itemId: string;
  name: string;
  price: number;
  isVeg: boolean | null;
  quantity: number;
}

export interface Cart {
  businessId: string;
  businessName: string;
  lines: CartLine[];
  /** Sent with checkout so a repeated tap returns the same order; renewed whenever the cart changes. */
  checkoutKey: string;
}

const STORAGE_KEY = 'th.cart';
export const MAX_QUANTITY = 50;

/** C-05: one business per cart, kept on the device so it survives closing the app. */
@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly state = signal<Cart | null>(load());

  readonly cart = this.state.asReadonly();
  readonly count = computed(() => this.state()?.lines.reduce((n, l) => n + l.quantity, 0) ?? 0);
  readonly subtotal = computed(
    () => this.state()?.lines.reduce((sum, l) => sum + l.price * l.quantity, 0) ?? 0,
  );

  quantity(itemId: string): number {
    return this.state()?.lines.find((l) => l.itemId === itemId)?.quantity ?? 0;
  }

  /** True when adding from this business would replace a cart from another one. */
  belongsToAnother(businessId: string): boolean {
    const cart = this.state();
    return cart !== null && cart.businessId !== businessId && cart.lines.length > 0;
  }

  add(business: { id: string; name: string }, item: Omit<CartLine, 'quantity'>): void {
    const current = this.state();
    const cart: Cart =
      current && current.businessId === business.id
        ? current
        : { businessId: business.id, businessName: business.name, lines: [], checkoutKey: '' };
    const existing = cart.lines.find((l) => l.itemId === item.itemId);
    const lines = existing
      ? cart.lines.map((l) =>
          l === existing ? { ...l, quantity: Math.min(MAX_QUANTITY, l.quantity + 1) } : l,
        )
      : [...cart.lines, { ...item, quantity: 1 }];
    this.save({ ...cart, lines });
  }

  setQuantity(itemId: string, quantity: number): void {
    const cart = this.state();
    if (!cart) {
      return;
    }
    const lines = cart.lines
      .map((l) => (l.itemId === itemId ? { ...l, quantity: Math.min(MAX_QUANTITY, quantity) } : l))
      .filter((l) => l.quantity > 0);
    this.save(lines.length ? { ...cart, lines } : null);
  }

  clear(): void {
    this.save(null);
  }

  private save(cart: Cart | null): void {
    const next = cart ? { ...cart, checkoutKey: crypto.randomUUID() } : null;
    this.state.set(next);
    try {
      if (next) {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
      } else {
        localStorage.removeItem(STORAGE_KEY);
      }
    } catch {
      // Storage can be blocked (private mode); the cart then lasts until the app closes.
    }
  }
}

function load(): Cart | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    const cart = raw ? (JSON.parse(raw) as Cart) : null;
    return cart && Array.isArray(cart.lines) && cart.lines.length ? cart : null;
  } catch {
    return null;
  }
}
