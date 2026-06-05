import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DriverTrip } from '../models/driver-trip.model';
import { CreateFuelLogDto } from '../models/fuel-log.model';

@Injectable({ providedIn: 'root' })
export class DriverAppService {
  private readonly base = `${environment.apiUrl}/driver-app`;

  constructor(private http: HttpClient) {}

  getTrips(): Observable<DriverTrip[]> {
    return this.http.get<DriverTrip[]>(`${this.base}/trips`);
  }

  startTrip(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/trips/${id}/start`, {});
  }

  completeTrip(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/trips/${id}/complete`, {});
  }

  rejectTrip(id: number, reason: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/trips/${id}/reject`, JSON.stringify(reason), {
      headers: { 'Content-Type': 'application/json' }
    });
  }

  submitFuelReceipt(fuelLog: CreateFuelLogDto): Observable<number> {
    return this.http.post<number>(`${this.base}/fuel-receipts`, fuelLog);
  }
}
