import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { InvoiceService } from '../../../core/services/invoice.service';
import { InvoiceDetail } from '../../../core/models/invoice.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { AuthService } from '../../../core/services/auth.service';
import { COMPANY_NAME, APP_LOGO_PATH } from '../../../core/constants/app-brand';

@Component({
  standalone: false,
  selector: 'app-invoice-detail',
  templateUrl: './invoice-detail.component.html',
  styleUrls: ['./invoice-detail.component.scss']
})
export class InvoiceDetailComponent implements OnInit {
  readonly companyName = COMPANY_NAME;
  readonly logoPath = APP_LOGO_PATH;
  invoice: InvoiceDetail | null = null;
  loading = true;
  error: string | null = null;
  acting = false;

  paymentStatusColors: Record<string, string> = {
    Pending: '#1565c0',
    Paid: '#2e7d32',
    Overdue: '#c62828'
  };

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private invoiceService: InvoiceService,
    private toast: UiToastService,
    private auth: AuthService
  ) {}

  ngOnInit(): void {
    const id = +this.route.snapshot.paramMap.get('id')!;
    this.invoiceService.getById(id).subscribe({
      next: inv => { this.invoice = inv; this.loading = false; },
      error: () => { this.loading = false; this.error = 'Failed to load invoice.'; }
    });
  }

  get canManage(): boolean {
    return this.auth.hasPermission('Invoice.Manage');
  }

  get canMarkPaid(): boolean {
    return !!this.invoice && this.invoice.paymentStatus !== 'Paid' && this.invoice.status !== 'Void';
  }

  print(): void { window.print(); }

  back(): void { void this.router.navigate(['/invoices']); }

  emailCustomer(): void {
    if (!this.invoice || this.acting) return;
    this.acting = true;
    this.invoiceService.emailToCustomer(this.invoice.id).subscribe({
      next: () => {
        this.toast.success('Invoice emailed to customer');
        this.acting = false;
        this.ngOnInit();
      },
      error: () => { this.acting = false; this.toast.error('Could not send invoice email'); }
    });
  }

  voidInvoice(): void {
    if (!this.invoice || this.acting) return;
    this.acting = true;
    this.invoiceService.void(this.invoice.id).subscribe({
      next: () => {
        this.toast.success('Invoice voided');
        this.acting = false;
        this.ngOnInit();
      },
      error: () => { this.acting = false; this.toast.error('Could not void invoice'); }
    });
  }

  markPaid(): void {
    if (!this.invoice || this.acting) return;
    this.acting = true;
    this.invoiceService.markPaid(this.invoice.id).subscribe({
      next: () => {
        this.toast.success('Invoice marked paid');
        this.acting = false;
        this.ngOnInit();
      },
      error: () => { this.acting = false; this.toast.error('Could not mark paid'); }
    });
  }
}
