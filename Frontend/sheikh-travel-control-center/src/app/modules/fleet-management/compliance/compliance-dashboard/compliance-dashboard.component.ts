import { Component, OnInit, inject } from '@angular/core';
import { FleetService } from '../../services/fleet.service';
import { ComplianceDocument } from '../../models/fleet.model';

@Component({
  selector: 'app-compliance-dashboard',
  templateUrl: './compliance-dashboard.component.html',
  styleUrls: ['./compliance-dashboard.component.scss']
})
export class ComplianceDashboardComponent implements OnInit {
  private readonly fleet = inject(FleetService);

  documents: ComplianceDocument[] = [];
  loading = true;

  ngOnInit(): void {
    this.fleet.getComplianceDocuments().subscribe({
      next: (docs) => {
        this.documents = docs;
        this.loading = false;
      },
      error: () => (this.loading = false)
    });
  }

  daysTo(date?: string): number | null {
    if (!date) return null;
    return Math.ceil((new Date(date).getTime() - Date.now()) / (1000 * 60 * 60 * 24));
  }

  get expiredCount(): number {
    return this.documents.filter((d) => { const x = this.daysTo(d.expiryDate); return x !== null && x < 0; }).length;
  }

  get expiringCount(): number {
    return this.documents.filter((d) => { const x = this.daysTo(d.expiryDate); return x !== null && x >= 0 && x <= 30; }).length;
  }

  get validCount(): number {
    return this.documents.filter((d) => { const x = this.daysTo(d.expiryDate); return x !== null && x > 30; }).length;
  }

  iconFor(type: string): string {
    const t = (type || '').toLowerCase();
    if (t.includes('insurance')) return 'health_and_safety';
    if (t.includes('registration')) return 'app_registration';
    if (t.includes('license')) return 'badge';
    if (t.includes('permit')) return 'description';
    return 'verified';
  }
}
