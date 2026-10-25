import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AdminSociety, Flat, ImportFlatsResult, Society, Tower } from './models';

@Injectable({ providedIn: 'root' })
export class SocietyApi {
  private readonly http = inject(HttpClient);

  listActive(): Promise<Society[]> {
    return firstValueFrom(this.http.get<Society[]>('/api/societies'));
  }

  towers(societyId: string): Promise<Tower[]> {
    return firstValueFrom(this.http.get<Tower[]>(`/api/societies/${societyId}/towers`));
  }
}

@Injectable({ providedIn: 'root' })
export class SocietyAdminApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/admin/societies';

  list(): Promise<AdminSociety[]> {
    return firstValueFrom(this.http.get<AdminSociety[]>(this.base));
  }

  create(name: string, city: string): Promise<AdminSociety> {
    return firstValueFrom(this.http.post<AdminSociety>(this.base, { name, city }));
  }

  setActive(societyId: string, active: boolean): Promise<unknown> {
    return firstValueFrom(
      this.http.post(`${this.base}/${societyId}/${active ? 'activate' : 'deactivate'}`, null),
    );
  }

  towers(societyId: string): Promise<Tower[]> {
    return firstValueFrom(this.http.get<Tower[]>(`${this.base}/${societyId}/towers`));
  }

  addTower(societyId: string, name: string): Promise<Tower> {
    return firstValueFrom(this.http.post<Tower>(`${this.base}/${societyId}/towers`, { name }));
  }

  deleteTower(societyId: string, towerId: string): Promise<unknown> {
    return firstValueFrom(this.http.delete(`${this.base}/${societyId}/towers/${towerId}`));
  }

  addFlat(societyId: string, towerId: string, number: string): Promise<Flat> {
    return firstValueFrom(
      this.http.post<Flat>(`${this.base}/${societyId}/towers/${towerId}/flats`, { number }),
    );
  }

  deleteFlat(societyId: string, flatId: string): Promise<unknown> {
    return firstValueFrom(this.http.delete(`${this.base}/${societyId}/flats/${flatId}`));
  }

  importFlats(societyId: string, csv: string): Promise<ImportFlatsResult> {
    return firstValueFrom(
      this.http.post<ImportFlatsResult>(`${this.base}/${societyId}/flats/import`, { csv }),
    );
  }
}
