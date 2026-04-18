import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { VehicleLocation, TrackingUpdate } from '../models/tracking.model';

@Injectable({ providedIn: 'root' })
export class TrackingService {
  private readonly base = `${environment.apiUrl}/tracking`;

  constructor(private http: HttpClient) {}

  getAllVehicleLocations(): Observable<VehicleLocation[]> {
    return this.http.get<VehicleLocation[]>(`${this.base}/locations`);
  }

  updateLocation(update: TrackingUpdate): Observable<void> {
    return this.http.post<void>(`${this.base}/update`, update);
  }
}
