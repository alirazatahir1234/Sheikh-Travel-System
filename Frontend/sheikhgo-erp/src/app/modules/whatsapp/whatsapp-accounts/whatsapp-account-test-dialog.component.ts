import { Component, Inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { WhatsAppAccount } from '../../../core/models/whatsapp.model';
import { WhatsAppInboxService } from '../../../core/services/whatsapp-inbox.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

export interface WhatsAppAccountTestDialogData {
  account: WhatsAppAccount;
}

@Component({
  standalone: false,
  selector: 'app-whatsapp-account-test-dialog',
  template: `
    <h2 mat-dialog-title>Send test message</h2>
    <mat-dialog-content>
      <p class="from-line">From <strong>{{ data.account.displayName }}</strong></p>
      <form [formGroup]="form" class="test-form" (ngSubmit)="send()">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Recipient</mat-label>
          <input matInput formControlName="recipient" placeholder="+9715… or +923…" autocomplete="tel" />
          <mat-hint>E.164 preferred (country code + number)</mat-hint>
          <mat-error *ngIf="form.get('recipient')?.hasError('required')">Recipient is required</mat-error>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Message</mat-label>
          <textarea matInput formControlName="text" rows="4" maxlength="4096"></textarea>
          <mat-hint align="end">{{ form.get('text')?.value?.length || 0 }} / 4096</mat-hint>
          <mat-error *ngIf="form.get('text')?.hasError('required')">Message is required</mat-error>
        </mat-form-field>
      </form>
      <div class="result ok" *ngIf="successMessage">{{ successMessage }}</div>
      <div class="result err" *ngIf="errorMessage">{{ errorMessage }}</div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close [disabled]="sending">Cancel</button>
      <button mat-raised-button color="primary" type="button" (click)="send()"
              [disabled]="form.invalid || sending">
        <mat-spinner *ngIf="sending" diameter="18" class="btn-spin"></mat-spinner>
        <span>{{ sending ? 'Sending…' : 'Send Test Message' }}</span>
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .from-line {
      margin: 0 0 1rem;
      color: #475569;
      font-size: 0.9rem;
    }
    .test-form {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
    .full { width: 100%; }
    .result {
      margin-top: 0.75rem;
      padding: 0.65rem 0.75rem;
      border-radius: 8px;
      font-size: 0.875rem;
      line-height: 1.4;
    }
    .result.ok {
      background: #ecfdf5;
      color: #166534;
    }
    .result.err {
      background: #fef2f2;
      color: #991b1b;
    }
    .btn-spin {
      display: inline-block;
      margin-right: 0.4rem;
    }
    mat-dialog-actions button {
      min-width: 7rem;
    }
  `]
})
export class WhatsAppAccountTestDialogComponent implements OnInit {
  form!: FormGroup;
  sending = false;
  successMessage: string | null = null;
  errorMessage: string | null = null;

  constructor(
    private readonly fb: FormBuilder,
    private readonly api: WhatsAppInboxService,
    private readonly toast: UiToastService,
    public dialogRef: MatDialogRef<WhatsAppAccountTestDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: WhatsAppAccountTestDialogData
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      recipient: ['', [Validators.required, Validators.maxLength(32)]],
      text: ['SheikhGo WhatsApp connectivity test', [Validators.required, Validators.maxLength(4096)]]
    });
  }

  send(): void {
    if (this.form.invalid || this.sending) {
      this.form.markAllAsTouched();
      return;
    }
    this.sending = true;
    this.successMessage = null;
    this.errorMessage = null;
    const { recipient, text } = this.form.getRawValue();
    this.api
      .sendOutbound({
        whatsAppAccountId: this.data.account.id,
        recipientPhoneNumber: String(recipient).trim(),
        text: String(text).trim()
      })
      .subscribe({
        next: result => {
          this.sending = false;
          this.successMessage = `Sent (${result.status}). Meta id: ${result.metaMessageId || 'n/a'}`;
          this.toast.success('Test message sent.');
        },
        error: err => {
          this.sending = false;
          this.errorMessage = err?.error?.message || 'Failed to send test message.';
          this.toast.error(this.errorMessage!);
        }
      });
  }
}
