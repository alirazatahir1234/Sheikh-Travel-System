import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface ContactLeadRequest {
  firstName: string;
  lastName: string;
  company: string;
  email: string;
  phone?: string;
  country?: string;
  fleetSize?: string;
  interestedIn?: string;
  message: string;
  website?: string;
}

export interface DemoLeadRequest {
  name: string;
  company: string;
  email: string;
  phone?: string;
  country?: string;
  vehicleCount?: string;
  currentGpsProvider?: string;
  interestedProduct?: string;
  message?: string;
  website?: string;
}

@Injectable({ providedIn: 'root' })
export class WebsiteLeadsService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl.replace(/\/$/, '');

  contact(body: ContactLeadRequest): Observable<{ success: boolean; message?: string }> {
    return this.http.post<unknown>(`${this.base}/public/contact`, body).pipe(
      map(() => ({ success: true, message: 'Message received.' })),
    );
  }

  requestDemo(body: DemoLeadRequest): Observable<{ success: boolean; message?: string }> {
    return this.http.post<unknown>(`${this.base}/public/request-demo`, body).pipe(
      map(() => ({ success: true, message: 'Demo request received.' })),
    );
  }
}
