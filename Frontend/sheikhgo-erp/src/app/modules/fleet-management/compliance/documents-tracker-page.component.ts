import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import {
  FleetEvAnomalyService,
  VehicleDocumentTrackerItem,
} from '../../../core/services/fleet-ev-anomaly.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { VehicleListItem } from '../../../core/models/vehicle.model';

const DOC_TYPES = [
  'Insurance',
  'Registration',
  'Fitness',
  'Permit',
  'Other',
] as const;

@Component({
  selector: 'app-documents-tracker-page',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, MatButtonModule],
  template: `
    <div class="tracker-page">
      <header class="tracker-header">
        <div>
          <h1>Insurance &amp; documents</h1>
          <p>Registration, insurance, fitness certificate, and compliance uploads with renewal status.</p>
        </div>
        <button mat-flat-button color="primary" type="button" (click)="startCreate()">
          <mat-icon>add</mat-icon>
          Add document
        </button>
      </header>

      @if (formOpen()) {
        <section class="editor" aria-label="Document editor">
          <h2>{{ editingId() ? 'Edit document' : 'New document' }}</h2>
          <div class="editor-grid">
            <label>
              Vehicle
              <select [(ngModel)]="form.vehicleId" [disabled]="!!editingId()">
                <option [ngValue]="0" disabled>Select vehicle</option>
                @for (v of vehicles(); track v.id) {
                  <option [ngValue]="v.id">{{ v.name || v.registrationNumber }} ({{ v.registrationNumber }})</option>
                }
              </select>
            </label>
            <label>
              Document type
              <select [(ngModel)]="form.documentType">
                @for (t of docTypes; track t) {
                  <option [ngValue]="t">{{ t }}</option>
                }
              </select>
            </label>
            <label>
              Document number
              <input type="text" [(ngModel)]="form.documentNumber" placeholder="Optional" />
            </label>
            <label>
              Issued
              <input type="date" [(ngModel)]="form.issuedDate" />
            </label>
            <label>
              Expiry
              <input type="date" [(ngModel)]="form.expiryDate" />
            </label>
            <label class="span-2">
              File URL
              <input type="url" [(ngModel)]="form.fileUrl" placeholder="https://…" />
            </label>
            <label class="span-2">
              Notes
              <input type="text" [(ngModel)]="form.notes" placeholder="Optional notes" />
            </label>
          </div>
          @if (saveError()) {
            <p class="error">{{ saveError() }}</p>
          }
          <div class="editor-actions">
            <button mat-button type="button" (click)="cancelEdit()" [disabled]="saving()">Cancel</button>
            <button mat-flat-button color="primary" type="button" (click)="save()" [disabled]="saving() || !canSave()">
              {{ saving() ? 'Saving…' : 'Save' }}
            </button>
          </div>
        </section>
      }

      @if (loading()) {
        <p>Loading…</p>
      } @else {
        <table>
          <thead>
            <tr>
              <th>Vehicle</th>
              <th>Document</th>
              <th>Expiry</th>
              <th>Status</th>
              <th>Source</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (d of items(); track trackRow(d)) {
              <tr [class.expired]="d.status === 'Expired'" [class.expiring]="d.status === 'Expiring'">
                <td>{{ d.vehicleName }}</td>
                <td>{{ d.documentType }}</td>
                <td>{{ d.expiryDate ? (d.expiryDate | date: 'mediumDate') : '—' }}</td>
                <td><span class="pill">{{ d.status }}</span></td>
                <td>{{ d.source }}</td>
                <td>
                  @if (d.complianceDocumentId) {
                    <button mat-button type="button" (click)="startEdit(d)">Edit</button>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6">No document expiry dates on file.</td></tr>
            }
          </tbody>
        </table>
      }
    </div>
  `,
  styles: [`
    .tracker-page { padding: 1.25rem; max-width: 1100px; }
    .tracker-header { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; margin-bottom: 1rem; }
    .tracker-header h1 { margin: 0 0 0.25rem; font-size: 1.5rem; }
    .tracker-header p { margin: 0; color: #64748b; }
    table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
    th, td { text-align: left; padding: 0.5rem 0.65rem; border-bottom: 1px solid #e2e8f0; }
    tr.expired { background: #fef2f2; }
    tr.expiring { background: #fffbeb; }
    .pill { font-size: 0.75rem; font-weight: 600; }
    .editor {
      background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 12px;
      padding: 1rem; margin-bottom: 1.25rem;
    }
    .editor h2 { margin: 0 0 0.75rem; font-size: 1.05rem; }
    .editor-grid {
      display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.75rem;
    }
    .editor-grid label { display: grid; gap: 0.25rem; font-size: 0.8rem; color: #475569; }
    .editor-grid .span-2 { grid-column: 1 / -1; }
    .editor-grid input, .editor-grid select {
      border: 1px solid #cbd5e1; border-radius: 8px; padding: 0.45rem 0.6rem; font: inherit; background: #fff;
    }
    .editor-actions { display: flex; justify-content: flex-end; gap: 0.5rem; margin-top: 0.85rem; }
    .error { color: #b91c1c; margin: 0.5rem 0 0; }
    @media (max-width: 720px) {
      .tracker-header { flex-direction: column; }
      .editor-grid { grid-template-columns: 1fr; }
      .editor-grid .span-2 { grid-column: auto; }
    }
  `],
})
export class DocumentsTrackerPageComponent implements OnInit {
  private readonly api = inject(FleetEvAnomalyService);
  private readonly vehiclesApi = inject(VehicleService);

  readonly docTypes = DOC_TYPES;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly formOpen = signal(false);
  readonly editingId = signal<number | null>(null);
  readonly saveError = signal<string | null>(null);
  readonly items = signal<VehicleDocumentTrackerItem[]>([]);
  readonly vehicles = signal<VehicleListItem[]>([]);

  form = this.emptyForm();

  ngOnInit(): void {
    this.reload();
    this.vehiclesApi.getAll(1, 200).subscribe({
      next: (page) => this.vehicles.set(page.items ?? []),
      error: () => this.vehicles.set([]),
    });
  }

  trackRow(d: VehicleDocumentTrackerItem): string {
    return `${d.vehicleId}-${d.documentType}-${d.complianceDocumentId ?? d.source}`;
  }

  startCreate(): void {
    this.editingId.set(null);
    this.form = this.emptyForm();
    this.saveError.set(null);
    this.formOpen.set(true);
  }

  startEdit(d: VehicleDocumentTrackerItem): void {
    if (!d.complianceDocumentId) return;
    this.editingId.set(d.complianceDocumentId);
    this.form = {
      vehicleId: d.vehicleId,
      documentType: d.documentType || 'Insurance',
      documentNumber: '',
      issuedDate: '',
      expiryDate: d.expiryDate ? d.expiryDate.slice(0, 10) : '',
      fileUrl: '',
      notes: '',
    };
    this.saveError.set(null);
    this.formOpen.set(true);
  }

  cancelEdit(): void {
    this.formOpen.set(false);
    this.editingId.set(null);
    this.saveError.set(null);
  }

  canSave(): boolean {
    return this.form.vehicleId > 0 && !!this.form.documentType?.trim();
  }

  save(): void {
    if (!this.canSave() || this.saving()) return;
    this.saving.set(true);
    this.saveError.set(null);
    this.api
      .upsertDocument({
        id: this.editingId(),
        vehicleId: this.form.vehicleId,
        documentType: this.form.documentType.trim(),
        documentNumber: this.form.documentNumber.trim() || null,
        issuedDate: this.form.issuedDate || null,
        expiryDate: this.form.expiryDate || null,
        fileUrl: this.form.fileUrl.trim() || null,
        notes: this.form.notes.trim() || null,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.formOpen.set(false);
          this.editingId.set(null);
          this.reload();
        },
        error: (err) => {
          this.saving.set(false);
          this.saveError.set(
            err?.error?.message || err?.message || 'Could not save document. Check Vehicle.Update permission and API.'
          );
        },
      });
  }

  private reload(): void {
    this.loading.set(true);
    this.api.getDocumentTracker().subscribe({
      next: (rows) => {
        this.items.set(rows ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private emptyForm() {
    return {
      vehicleId: 0,
      documentType: 'Insurance' as string,
      documentNumber: '',
      issuedDate: '',
      expiryDate: '',
      fileUrl: '',
      notes: '',
    };
  }
}
