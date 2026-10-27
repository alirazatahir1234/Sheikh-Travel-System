import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import {
  SosDispatchService,
  SosEventRow,
  SosRecoveryPartner,
} from '../../../core/services/sos-dispatch.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-sos-dispatch',
  templateUrl: './sos-dispatch.component.html',
  styleUrls: ['./sos-dispatch.component.scss'],
})
export class SosDispatchComponent implements OnInit, OnDestroy {
  tab: 'events' | 'partners' = 'events';
  events: SosEventRow[] = [];
  partners: SosRecoveryPartner[] = [];
  loading = false;
  statusFilter: string | null = 'Open';
  selectedEvent: SosEventRow | null = null;
  showPartnerForm = false;
  editingPartnerId: number | null = null;
  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  partnerForm!: ReturnType<FormBuilder['group']>;

  readonly regionOptions = [
    { value: 'PK', label: 'Pakistan' },
    { value: 'AE', label: 'Dubai / UAE' },
    { value: 'SA', label: 'Saudi Arabia' },
  ];

  readonly partnerTypes = ['Towing', 'FlatTire', 'Medical'];

  constructor(
    private sos: SosDispatchService,
    private fb: FormBuilder,
    private toast: UiToastService,
  ) {
    this.partnerForm = this.fb.group({
      regionCode: ['PK', Validators.required],
      partnerType: ['Towing', Validators.required],
      name: ['', Validators.required],
      phone: ['', Validators.required],
      email: [''],
      notes: [''],
      isActive: [true],
    });
  }

  ngOnInit(): void {
    this.load();
    this.refreshTimer = setInterval(() => this.loadEvents(), 20_000);
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) clearInterval(this.refreshTimer);
  }

  load(): void {
    this.loading = true;
    this.loadEvents();
    this.sos.listPartners().subscribe({
      next: p => {
        this.partners = p;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.toast.error('Could not load SOS partners.');
      },
    });
  }

  loadEvents(): void {
    this.sos.listEvents(this.statusFilter ?? undefined).subscribe({
      next: rows => {
        this.events = rows;
        if (this.selectedEvent) {
          this.selectedEvent = rows.find(e => e.id === this.selectedEvent!.id) ?? null;
        }
      },
      error: () => this.toast.error('Could not load SOS events.'),
    });
  }

  resolve(ev: SosEventRow): void {
    this.sos.resolveEvent(ev.id).subscribe({
      next: ok => {
        if (ok) {
          this.toast.success('SOS resolved.');
          this.loadEvents();
        }
      },
      error: () => this.toast.error('Resolve failed.'),
    });
  }

  openPartnerForm(partner?: SosRecoveryPartner): void {
    this.showPartnerForm = true;
    this.editingPartnerId = partner?.id ?? null;
    this.partnerForm.reset({
      regionCode: partner?.regionCode ?? 'PK',
      partnerType: partner?.partnerType ?? 'Towing',
      name: partner?.name ?? '',
      phone: partner?.phone ?? '',
      email: partner?.email ?? '',
      notes: partner?.notes ?? '',
      isActive: partner?.isActive ?? true,
    });
  }

  savePartner(): void {
    if (this.partnerForm.invalid) return;
    const v = this.partnerForm.getRawValue();
    this.sos
      .savePartner(
        {
          regionCode: v.regionCode!,
          partnerType: v.partnerType!,
          name: v.name!.trim(),
          phone: v.phone!.trim(),
          email: v.email?.trim() || undefined,
          notes: v.notes?.trim() || undefined,
          isActive: !!v.isActive,
        },
        this.editingPartnerId ?? undefined,
      )
      .subscribe({
        next: () => {
          this.toast.success('Partner saved.');
          this.showPartnerForm = false;
          this.load();
        },
        error: () => this.toast.error('Save failed.'),
      });
  }

  deletePartner(p: SosRecoveryPartner): void {
    if (!confirm(`Remove partner "${p.name}"?`)) return;
    this.sos.deletePartner(p.id).subscribe({
      next: () => {
        this.toast.success('Partner removed.');
        this.load();
      },
      error: () => this.toast.error('Delete failed.'),
    });
  }

  mapsLink(ev: SosEventRow): string | null {
    if (ev.latitude == null || ev.longitude == null) return null;
    return `https://www.google.com/maps?q=${ev.latitude},${ev.longitude}`;
  }
}
