import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TrackerBrand, TrackerModel } from '../models/tracker-catalog.model';

@Injectable({ providedIn: 'root' })
export class TrackerCatalogService {
  private readonly base = `${environment.apiUrl}`;

  constructor(private http: HttpClient) {}

  getBrands(): Observable<TrackerBrand[]> {
    return this.http.get<TrackerBrand[]>(`${this.base}/tracker-brands`);
  }

  getModels(brandId?: number): Observable<TrackerModel[]> {
    const params: Record<string, string> = {};
    if (brandId) params['brandId'] = String(brandId);
    return this.http.get<TrackerModel[]>(`${this.base}/tracker-models`, { params });
  }
}
