import { Component } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';

@Component({
  standalone: false,
  selector: 'app-reset-database-dialog',
  template: `
    <h2 mat-dialog-title>
      <mat-icon class="warn-icon">warning</mat-icon>
      Reset Database
    </h2>
    <mat-dialog-content>
      <p class="lead">This operation cannot be undone.</p>
      <p>The following data will be deleted:</p>
      <ul class="delete-list">
        <li *ngFor="let item of deletedItems">{{ item }}</li>
      </ul>
      <p class="confirm-label">Type <strong>RESET</strong> to continue.</p>
      <mat-form-field appearance="outline" class="confirm-field">
        <mat-label>Confirmation</mat-label>
        <input matInput [(ngModel)]="confirmation" autocomplete="off" spellcheck="false" />
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="onCancel()">Cancel</button>
      <button
        mat-flat-button
        color="warn"
        type="button"
        [disabled]="confirmation.trim().toUpperCase() !== 'RESET'"
        (click)="onConfirm()">
        Delete Everything
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    h2[mat-dialog-title] {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0;
    }

    .warn-icon {
      color: #dc2626;
    }

    .lead {
      font-weight: 600;
      color: #b91c1c;
      margin: 0 0 12px;
    }

    p {
      margin: 0 0 8px;
      color: #475569;
      line-height: 1.5;
    }

    .delete-list {
      margin: 0 0 16px;
      padding-left: 20px;
      columns: 2;
      color: #64748b;
      font-size: 13px;
    }

    .confirm-label {
      margin-top: 8px;
    }

    .confirm-field {
      width: 100%;
      margin-top: 8px;
    }

    mat-dialog-actions {
      padding-top: 8px;
    }
  `]
})
export class ResetDatabaseDialogComponent {
  confirmation = '';

  readonly deletedItems = [
    'Companies',
    'Users',
    'Drivers',
    'Customers',
    'Vehicles',
    'Trips',
    'Bookings',
    'Routes',
    'GPS History',
    'Fuel Logs',
    'Attendance',
    'Documents',
    'Notifications',
    'Payments',
    'Audit Logs',
    'Files',
    'Reports'
  ];

  constructor(private dialogRef: MatDialogRef<ResetDatabaseDialogComponent>) {}

  onCancel(): void {
    this.dialogRef.close(null);
  }

  onConfirm(): void {
    if (this.confirmation.trim().toUpperCase() !== 'RESET') return;
    this.dialogRef.close('RESET');
  }
}
