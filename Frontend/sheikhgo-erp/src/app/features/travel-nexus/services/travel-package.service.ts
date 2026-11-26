import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { SaveTravelHotel, SaveTravelPackage, TravelHotel, TravelHotelCity, TravelPackage, TravelPackageListItem, TravelPackageStatus } from '../models/travel.models';

/** Hotel catalog and packages (`api/travel/hotels`, `api/travel/packages`). */
@Injectable({ providedIn: 'root' })
export class TravelPackageService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel`;

  getHotels(city?: TravelHotelCity, isActive?: boolean): Observable<TravelHotel[]> {
    let params = new HttpParams();
    if (city) params = params.set('city', city);
    if (isActive !== undefined) params = params.set('isActive', isActive);
    return this.http.get<TravelHotel[]>(`${this.base}/hotels`, { params });
  }
  createHotel(dto: SaveTravelHotel): Observable<number> { return this.http.post<number>(`${this.base}/hotels`, dto); }
  updateHotel(id: number, dto: SaveTravelHotel): Observable<number> { return this.http.put<number>(`${this.base}/hotels/${id}`, dto); }
  deleteHotel(id: number): Observable<boolean> { return this.http.delete<boolean>(`${this.base}/hotels/${id}`); }

  getPackages(status?: TravelPackageStatus): Observable<TravelPackageListItem[]> {
    const params = status ? new HttpParams().set('status', status) : undefined;
    return this.http.get<TravelPackageListItem[]>(`${this.base}/packages`, { params });
  }
  getPackage(id: number): Observable<TravelPackage> { return this.http.get<TravelPackage>(`${this.base}/packages/${id}`); }
  createPackage(dto: SaveTravelPackage): Observable<number> { return this.http.post<number>(`${this.base}/packages`, dto); }
  updatePackage(id: number, dto: SaveTravelPackage): Observable<number> { return this.http.put<number>(`${this.base}/packages/${id}`, dto); }
  publish(id: number): Observable<string> { return this.http.post<string>(`${this.base}/packages/${id}/publish`, {}); }
  archive(id: number): Observable<string> { return this.http.post<string>(`${this.base}/packages/${id}/archive`, {}); }
  deletePackage(id: number): Observable<boolean> { return this.http.delete<boolean>(`${this.base}/packages/${id}`); }
}
