import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { SaveTravelChecklist, SaveTravelChecklistItem, TravelChecklist, TravelChecklistListItem, TravelChecklistStatus } from '../models/travel.models';

/** Document checklists API (`api/travel/checklists`). Only drafts are editable. */
@Injectable({ providedIn: 'root' })
export class TravelChecklistService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/travel/checklists`;

  getChecklists(countryCode?: string, status?: TravelChecklistStatus): Observable<TravelChecklistListItem[]> {
    let params = new HttpParams();
    if (countryCode) params = params.set('countryCode', countryCode);
    if (status) params = params.set('status', status);
    return this.http.get<TravelChecklistListItem[]>(this.base, { params });
  }
  getChecklist(id: number): Observable<TravelChecklist> { return this.http.get<TravelChecklist>(`${this.base}/${id}`); }
  create(dto: SaveTravelChecklist): Observable<number> { return this.http.post<number>(this.base, dto); }
  update(id: number, dto: SaveTravelChecklist): Observable<boolean> { return this.http.put<boolean>(`${this.base}/${id}`, dto); }
  saveItems(id: number, items: SaveTravelChecklistItem[]): Observable<boolean> { return this.http.put<boolean>(`${this.base}/${id}/items`, items); }
  activate(id: number): Observable<boolean> { return this.http.post<boolean>(`${this.base}/${id}/activate`, {}); }
  newVersion(id: number): Observable<number> { return this.http.post<number>(`${this.base}/${id}/new-version`, {}); }
  delete(id: number): Observable<boolean> { return this.http.delete<boolean>(`${this.base}/${id}`); }
}
