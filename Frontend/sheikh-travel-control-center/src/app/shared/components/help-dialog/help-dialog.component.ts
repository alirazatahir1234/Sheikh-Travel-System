import { Component } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';

interface HelpSection {
  title: string;
  icon: string;
  items: string[];
}

@Component({
  selector: 'app-help-dialog',
  template: `
    <h2 mat-dialog-title>
      <mat-icon>help_outline</mat-icon>
      Help & Support
    </h2>
    <mat-dialog-content>
      <div class="help-intro">
        <p>Welcome to the Sheikh Travel System! Here are some quick guides to help you get started.</p>
      </div>

      <div class="help-section" *ngFor="let section of sections">
        <h3><mat-icon>{{ section.icon }}</mat-icon> {{ section.title }}</h3>
        <ul>
          <li *ngFor="let item of section.items">{{ item }}</li>
        </ul>
      </div>

      <div class="help-contact">
        <h3><mat-icon>contact_support</mat-icon> Need More Help?</h3>
        <p>Contact our support team:</p>
        <ul>
          <li><strong>Email:</strong> support&#64;sheikhtravel.com</li>
          <li><strong>Phone:</strong> +92 300 1234567</li>
        </ul>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="close()">Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    h2[mat-dialog-title] {
      display: flex;
      align-items: center;
      gap: 8px;
      mat-icon { color: #3b82f6; }
    }
    .help-intro {
      padding: 8px 0 16px;
      color: #64748b;
      border-bottom: 1px solid #e2e8f0;
      margin-bottom: 16px;
    }
    .help-section, .help-contact {
      margin-bottom: 20px;
      h3 {
        display: flex;
        align-items: center;
        gap: 8px;
        font-size: 14px;
        font-weight: 600;
        color: #1e293b;
        margin: 0 0 8px;
        mat-icon { font-size: 18px; width: 18px; height: 18px; color: #64748b; }
      }
      ul {
        margin: 0;
        padding-left: 26px;
        li {
          color: #475569;
          font-size: 13px;
          line-height: 1.8;
        }
      }
    }
    .help-contact {
      background: #f8fafc;
      border-radius: 8px;
      padding: 12px 16px;
      ul { list-style: none; padding-left: 0; }
    }
  `]
})
export class HelpDialogComponent {
  sections: HelpSection[] = [
    {
      title: 'Creating a Booking',
      icon: 'confirmation_number',
      items: [
        'Navigate to Bookings → New Booking',
        'Select a customer (or create a new one)',
        'Choose pickup/dropoff locations and dates',
        'Assign a vehicle and driver',
        'Review and confirm the booking'
      ]
    },
    {
      title: 'Managing Vehicles',
      icon: 'directions_bus',
      items: [
        'Add new vehicles from Vehicles → Add Vehicle',
        'Track maintenance schedules under Maintenance',
        'Log fuel entries in Fuel Logs',
        'Monitor vehicle locations on the Tracking map'
      ]
    },
    {
      title: 'Managing Drivers',
      icon: 'badge',
      items: [
        'Add drivers with license and contact details',
        'View driver profiles for trip history',
        'Track driver availability and status',
        'Monitor license expiration alerts'
      ]
    },
    {
      title: 'Reports & Analytics',
      icon: 'insights',
      items: [
        'Access Reports for revenue and trip analytics',
        'Export data to Excel or PDF',
        'View Audit Logs for system activity (Admin)',
        'Monitor KPIs on the Dashboard'
      ]
    }
  ];

  constructor(private dialogRef: MatDialogRef<HelpDialogComponent>) {}

  close(): void {
    this.dialogRef.close();
  }
}
