import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  AdminBusiness,
  BusinessPage,
  BusinessSettings,
  BusinessStatus,
  BusinessType,
  Category,
  Directory,
  OwnerBusiness,
  OwnerItem,
  Photo,
  SaveBusinessRequest,
  SaveItemRequest,
} from './models';

function photoForm(photo: Blob): FormData {
  const form = new FormData();
  form.append('file', photo, 'photo.jpg');
  return form;
}

@Injectable({ providedIn: 'root' })
export class BusinessTypeApi {
  private readonly http = inject(HttpClient);

  /** Active types and categories, for registration. */
  list(): Promise<BusinessType[]> {
    return firstValueFrom(this.http.get<BusinessType[]>('/api/business-types'));
  }
}

/** The signed-in resident's own business (B-01, B-02, B-03). */
@Injectable({ providedIn: 'root' })
export class OwnerApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/my/business';

  get(): Promise<OwnerBusiness | null> {
    return firstValueFrom(this.http.get<OwnerBusiness | null>(this.base));
  }

  register(request: SaveBusinessRequest): Promise<OwnerBusiness> {
    return firstValueFrom(this.http.post<OwnerBusiness>(this.base, request));
  }

  update(request: SaveBusinessRequest): Promise<OwnerBusiness> {
    return firstValueFrom(this.http.put<OwnerBusiness>(this.base, request));
  }

  saveSettings(settings: Omit<BusinessSettings, 'isOpen'>): Promise<OwnerBusiness> {
    return firstValueFrom(this.http.put<OwnerBusiness>(`${this.base}/settings`, settings));
  }

  setOpen(isOpen: boolean): Promise<OwnerBusiness> {
    return firstValueFrom(this.http.post<OwnerBusiness>(`${this.base}/open`, { isOpen }));
  }

  addPhoto(photo: Blob): Promise<Photo> {
    return firstValueFrom(this.http.post<Photo>(`${this.base}/photos`, photoForm(photo)));
  }

  deletePhoto(photoId: string): Promise<unknown> {
    return firstValueFrom(this.http.delete(`${this.base}/photos/${photoId}`));
  }

  items(): Promise<OwnerItem[]> {
    return firstValueFrom(this.http.get<OwnerItem[]>(`${this.base}/items`));
  }

  createItem(request: SaveItemRequest): Promise<OwnerItem> {
    return firstValueFrom(this.http.post<OwnerItem>(`${this.base}/items`, request));
  }

  updateItem(itemId: string, request: SaveItemRequest): Promise<OwnerItem> {
    return firstValueFrom(this.http.put<OwnerItem>(`${this.base}/items/${itemId}`, request));
  }

  setAvailable(itemId: string, isAvailableToday: boolean): Promise<OwnerItem> {
    return firstValueFrom(
      this.http.post<OwnerItem>(`${this.base}/items/${itemId}/availability`, { isAvailableToday }),
    );
  }

  deleteItem(itemId: string): Promise<unknown> {
    return firstValueFrom(this.http.delete(`${this.base}/items/${itemId}`));
  }

  setItemPhoto(itemId: string, photo: Blob): Promise<OwnerItem> {
    return firstValueFrom(
      this.http.post<OwnerItem>(`${this.base}/items/${itemId}/photo`, photoForm(photo)),
    );
  }

  removeItemPhoto(itemId: string): Promise<OwnerItem> {
    return firstValueFrom(this.http.delete<OwnerItem>(`${this.base}/items/${itemId}/photo`));
  }
}

/** What residents browse (C-02, C-03, C-04). */
@Injectable({ providedIn: 'root' })
export class DirectoryApi {
  private readonly http = inject(HttpClient);

  list(): Promise<Directory> {
    return firstValueFrom(this.http.get<Directory>('/api/directory/businesses'));
  }

  get(businessId: string): Promise<BusinessPage> {
    return firstValueFrom(this.http.get<BusinessPage>(`/api/directory/businesses/${businessId}`));
  }
}

/** Platform admin: approvals (A-02) and types and categories (A-03). */
@Injectable({ providedIn: 'root' })
export class BusinessAdminApi {
  private readonly http = inject(HttpClient);

  list(status: BusinessStatus): Promise<AdminBusiness[]> {
    return firstValueFrom(
      this.http.get<AdminBusiness[]>('/api/admin/businesses', { params: { status } }),
    );
  }

  decide(
    businessId: string,
    decision: 'approve' | 'reject' | 'deactivate',
    note: string | null,
  ): Promise<unknown> {
    return firstValueFrom(
      this.http.post(`/api/admin/businesses/${businessId}/${decision}`, { note }),
    );
  }

  types(): Promise<BusinessType[]> {
    return firstValueFrom(this.http.get<BusinessType[]>('/api/admin/business-types'));
  }

  createType(type: Omit<BusinessType, 'id' | 'categories'>): Promise<BusinessType> {
    return firstValueFrom(this.http.post<BusinessType>('/api/admin/business-types', type));
  }

  updateType(typeId: string, type: Omit<BusinessType, 'id' | 'categories'>): Promise<unknown> {
    return firstValueFrom(this.http.put(`/api/admin/business-types/${typeId}`, type));
  }

  createCategory(typeId: string, category: Omit<Category, 'id'>): Promise<Category> {
    return firstValueFrom(
      this.http.post<Category>(`/api/admin/business-types/${typeId}/categories`, category),
    );
  }

  updateCategory(categoryId: string, category: Omit<Category, 'id'>): Promise<unknown> {
    return firstValueFrom(
      this.http.put(`/api/admin/business-types/categories/${categoryId}`, category),
    );
  }
}
