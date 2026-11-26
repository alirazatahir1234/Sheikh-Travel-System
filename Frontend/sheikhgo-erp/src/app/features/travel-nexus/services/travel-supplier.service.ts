import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { SaveTravelSupplier, TravelAirport, TravelSupplier, TravelSupplierType } from '../models/travel.models';

/** Suppliers (`api/travel/suppliers`) and the airport lookup (`api/travel/airports`). */
@Injectable({ providedIn: 'root' })
export class TravelSupplierService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel`;

  /** Airports are static reference data: one request per session. */
  readonly airports$: Observable<TravelAirport[]> = this.http.get<TravelAirport[]>(`${this.base}/airports`)
    .pipe(shareReplay({ bufferSize: 1, refCount: true }));

  getSuppliers(supplierType?: TravelSupplierType, isActive?: boolean): Observable<TravelSupplier[]> {
    let params = new HttpParams();
    if (supplierType) params = params.set('supplierType', supplierType);
    if (isActive !== undefined) params = params.set('isActive', isActive);
    return this.http.get<TravelSupplier[]>(`${this.base}/suppliers`, { params });
  }
  create(dto: SaveTravelSupplier): Observable<number> { return this.http.post<number>(`${this.base}/suppliers`, dto); }
  update(id: number, dto: SaveTravelSupplier): Observable<number> { return this.http.put<number>(`${this.base}/suppliers/${id}`, dto); }
  delete(id: number): Observable<boolean> { return this.http.delete<boolean>(`${this.base}/suppliers/${id}`); }
}
