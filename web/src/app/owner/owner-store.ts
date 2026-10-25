import { Injectable, inject, signal } from '@angular/core';
import { OwnerApi } from '../core/api/business.api';
import { OwnerBusiness } from '../core/api/models';

/** The owner's business, shared by the owner screens so each does not reload it. */
@Injectable({ providedIn: 'root' })
export class OwnerStore {
  private readonly api = inject(OwnerApi);
  private loaded = false;

  readonly business = signal<OwnerBusiness | null>(null);

  async load(force = false): Promise<OwnerBusiness | null> {
    if (force || !this.loaded) {
      this.business.set(await this.api.get());
      this.loaded = true;
    }
    return this.business();
  }

  set(business: OwnerBusiness): void {
    this.business.set(business);
    this.loaded = true;
  }
}
