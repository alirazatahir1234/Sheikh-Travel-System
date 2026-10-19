import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup } from '@angular/forms';
import { SettlementService } from '../../../core/services/settlement.service';
import { SettlementDetail } from '../../../core/models/settlement.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  standalone: false,
  selector: 'app-settlement-detail',
  templateUrl: './settlement-detail.component.html',
  styleUrls: ['./settlement-detail.component.scss']
})
export class SettlementDetailComponent implements OnInit {
  settlement: SettlementDetail | null = null;
  loading = true;
  error: string | null = null;
  acting = false;
  paidForm: FormGroup;
  adjustForm: FormGroup;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private settlementService: SettlementService,
    private toast: UiToastService,
    private auth: AuthService,
    private fb: FormBuilder
  ) {
    this.paidForm = this.fb.group({ reference: [''] });
    this.adjustForm = this.fb.group({ adjustments: [0], notes: [''] });
  }

  get canManage(): boolean {
    return this.auth.hasPermission('Settlement.Manage');
  }

  ngOnInit(): void { this.reload(); }

  reload(): void {
    const id = +this.route.snapshot.paramMap.get('id')!;
    this.loading = true;
    this.settlementService.getById(id).subscribe({
      next: s => {
        this.settlement = s;
        this.adjustForm.patchValue({ adjustments: s.adjustments, notes: s.notes || '' });
        this.loading = false;
      },
      error: () => { this.loading = false; this.error = 'Failed to load settlement.'; }
    });
  }

  back(): void { void this.router.navigate(['/settlements']); }

  approve(): void {
    if (!this.settlement || this.acting) return;
    this.acting = true;
    this.settlementService.approve(this.settlement.id).subscribe({
      next: () => { this.acting = false; this.toast.success('Approved'); this.reload(); },
      error: () => { this.acting = false; this.toast.error('Approve failed'); }
    });
  }

  markPaid(): void {
    if (!this.settlement || this.acting) return;
    this.acting = true;
    this.settlementService.markPaid(this.settlement.id, this.paidForm.value.reference || undefined).subscribe({
      next: () => { this.acting = false; this.toast.success('Marked paid'); this.reload(); },
      error: () => { this.acting = false; this.toast.error('Mark paid failed'); }
    });
  }

  adjust(): void {
    if (!this.settlement || this.acting) return;
    this.acting = true;
    const v = this.adjustForm.value;
    this.settlementService.adjust(this.settlement.id, +v.adjustments || 0, v.notes || undefined).subscribe({
      next: () => { this.acting = false; this.toast.success('Adjusted'); this.reload(); },
      error: () => { this.acting = false; this.toast.error('Adjust failed'); }
    });
  }
}
