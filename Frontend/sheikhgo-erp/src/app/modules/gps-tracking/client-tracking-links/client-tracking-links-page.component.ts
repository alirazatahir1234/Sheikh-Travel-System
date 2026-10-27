import { Component, OnInit } from '@angular/core';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import { ClientTrackingLink, ClientTrackingLinkCreated } from '../../../core/models/gps-tracking.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-client-tracking-links-page',
  templateUrl: './client-tracking-links-page.component.html',
  styleUrls: ['./client-tracking-links-page.component.scss']
})
export class ClientTrackingLinksPageComponent implements OnInit {
  loading = false;
  loadError: string | null = null;
  rows: ClientTrackingLink[] = [];
  tripIdInput: number | null = null;
  hoursValid = 48;
  created: ClientTrackingLinkCreated | null = null;
  createError: string | null = null;

  constructor(private gps: GpsTrackingService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.loadError = null;
    this.gps.listClientTrackingLinks(100).subscribe({
      next: rows => {
        this.rows = rows ?? [];
        this.loading = false;
      },
      error: err => {
        this.loadError = apiErrorMessage(err);
        this.loading = false;
      }
    });
  }

  createLink(): void {
    if (!this.tripIdInput || this.tripIdInput < 1) {
      this.createError = 'Enter a valid trip ID.';
      return;
    }
    this.createError = null;
    this.created = null;
    this.gps.createClientTrackingLink(this.tripIdInput, this.hoursValid).subscribe({
      next: res => {
        this.created = res;
        this.load();
      },
      error: err => {
        this.createError = apiErrorMessage(err);
      }
    });
  }

  revoke(row: ClientTrackingLink): void {
    if (!row.isActive) return;
    this.gps.revokeClientTrackingLink(row.id).subscribe({
      next: () => this.load(),
      error: err => {
        this.loadError = apiErrorMessage(err);
      }
    });
  }

  fullUrl(tokenPath: string): string {
    if (typeof window === 'undefined') return tokenPath;
    const origin = window.location.origin;
    return `${origin}${tokenPath.replace('{token}', '')}`;
  }

  copyCreated(): void {
    if (!this.created || typeof navigator === 'undefined') return;
    const url = `${window.location.origin}${this.created.publicUrl}`;
    void navigator.clipboard?.writeText(url);
  }
}
