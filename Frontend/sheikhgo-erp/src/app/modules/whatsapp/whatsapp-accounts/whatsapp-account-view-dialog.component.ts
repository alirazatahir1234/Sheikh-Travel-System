import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { WhatsAppAccount } from '../../../core/models/whatsapp.model';

export interface WhatsAppAccountViewDialogData {
  account: WhatsAppAccount;
  formattedPhone: string;
  countryLabel: string;
  purposeLabel: string;
  healthLabel: string;
}

@Component({
  standalone: false,
  selector: 'app-whatsapp-account-view-dialog',
  template: `
    <h2 mat-dialog-title>Account details</h2>
    <mat-dialog-content>
      <dl class="view-grid">
        <div>
          <dt>Name</dt>
          <dd>{{ data.account.displayName }}</dd>
        </div>
        <div>
          <dt>Phone number</dt>
          <dd>{{ data.formattedPhone }}</dd>
        </div>
        <div>
          <dt>Country</dt>
          <dd>{{ data.countryLabel }}</dd>
        </div>
        <div>
          <dt>Purpose</dt>
          <dd>{{ data.purposeLabel }}</dd>
        </div>
        <div>
          <dt>Status</dt>
          <dd>{{ data.account.status || (data.account.isActive ? 'Active' : 'Inactive') }}</dd>
        </div>
        <div>
          <dt>Active</dt>
          <dd>{{ data.account.isActive ? 'Yes' : 'No' }}</dd>
        </div>
        <div>
          <dt>Default</dt>
          <dd>{{ data.account.isDefault ? 'Yes' : 'No' }}</dd>
        </div>
        <div>
          <dt>Credentials configured</dt>
          <dd>{{ data.account.hasAccessToken ? 'Yes' : 'No' }}</dd>
        </div>
        <div class="full">
          <dt>Last health check</dt>
          <dd>{{ data.healthLabel }}</dd>
        </div>
        <div class="full" *ngIf="data.account.lastHealthMessage">
          <dt>Health message</dt>
          <dd>{{ data.account.lastHealthMessage }}</dd>
        </div>
      </dl>
      <p class="hint">Access tokens and Authorization headers are never shown in the UI.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .view-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.85rem 1rem;
      margin: 0;
    }
    .view-grid .full { grid-column: 1 / -1; }
    dt {
      margin: 0;
      font-size: 0.72rem;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: #64748b;
    }
    dd {
      margin: 0.2rem 0 0;
      color: #0f172a;
      font-size: 0.95rem;
      word-break: break-word;
    }
    .hint {
      margin: 1rem 0 0;
      font-size: 0.8rem;
      color: #64748b;
    }
    @media (max-width: 480px) {
      .view-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class WhatsAppAccountViewDialogComponent {
  constructor(
    public dialogRef: MatDialogRef<WhatsAppAccountViewDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: WhatsAppAccountViewDialogData
  ) {}
}
