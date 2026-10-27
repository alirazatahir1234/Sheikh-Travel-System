import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { DriverWalletService } from '../../../core/services/driver-wallet.service';
import {
  DriverCashOutPreview,
  DriverCashOutRequest,
  DriverWallet,
  DriverWalletLedgerEntry,
  DriverWalletPolicy
} from '../../../core/models/driver-wallet.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-driver-wallet-page',
  templateUrl: './driver-wallet-page.component.html',
  styleUrls: ['./driver-wallet-page.component.scss']
})
export class DriverWalletPageComponent implements OnInit {
  wallet: DriverWallet | null = null;
  policy: DriverWalletPolicy | null = null;
  preview: DriverCashOutPreview | null = null;
  ledger: DriverWalletLedgerEntry[] = [];
  cashOuts: DriverCashOutRequest[] = [];
  loading = true;
  submitting = false;
  savingPayout = false;

  payoutForm: FormGroup;
  cashOutForm: FormGroup;

  methods = [
    { value: 'BankTransfer', label: 'Bank transfer' },
    { value: 'JazzCash', label: 'JazzCash' },
    { value: 'EasyPaisa', label: 'Easypaisa' }
  ];

  constructor(
    private fb: FormBuilder,
    private walletService: DriverWalletService,
    private toast: UiToastService
  ) {
    this.payoutForm = this.fb.group({
      bankAccountName: [''],
      bankAccountNumber: [''],
      bankName: [''],
      jazzCashNumber: [''],
      easypaisaNumber: [''],
      cashOutCadence: ['Weekly', Validators.required]
    });

    this.cashOutForm = this.fb.group({
      grossAmount: [null, [Validators.required, Validators.min(0.01)]],
      method: ['BankTransfer', Validators.required],
      notes: ['']
    });
  }

  ngOnInit(): void {
    this.walletService.getDriverPolicy().subscribe({
      next: p => {
        this.policy = p;
        const min = p.minimumCashOutAmountPkr ?? 500;
        this.cashOutForm.get('grossAmount')?.addValidators(Validators.min(min));
      },
      error: () => undefined
    });

    this.cashOutForm.get('grossAmount')?.valueChanges.pipe(
      debounceTime(250),
      distinctUntilChanged()
    ).subscribe(v => this.refreshPreview(Number(v)));

    this.loadAll();
  }

  loadAll(): void {
    this.loading = true;
    this.walletService.getMyWallet().subscribe({
      next: w => {
        this.wallet = w;
        const p = w.payoutDetails;
        this.payoutForm.patchValue({
          bankAccountName: p.bankAccountName ?? '',
          bankAccountNumber: p.bankAccountNumber ?? '',
          bankName: p.bankName ?? '',
          jazzCashNumber: p.jazzCashNumber ?? '',
          easypaisaNumber: p.easypaisaNumber ?? '',
          cashOutCadence: w.cashOutCadence ?? 'Weekly'
        });
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.toast.error('Could not load wallet.');
      }
    });

    this.walletService.getMyLedger(1, 30).subscribe({
      next: r => { this.ledger = r.items; },
      error: () => undefined
    });

    this.walletService.getMyCashOutRequests(1, 30).subscribe({
      next: r => { this.cashOuts = r.items; },
      error: () => undefined
    });
  }

  refreshPreview(gross: number): void {
    if (!gross || gross <= 0) {
      this.preview = null;
      return;
    }
    this.walletService.previewCashOut(gross, true).subscribe({
      next: p => { this.preview = p; },
      error: () => { this.preview = null; }
    });
  }

  savePayoutDetails(): void {
    if (this.payoutForm.invalid || this.savingPayout) return;
    this.savingPayout = true;
    this.walletService.updateMyPayoutDetails(this.payoutForm.value).subscribe({
      next: () => {
        this.savingPayout = false;
        this.toast.success('Payout details saved.');
        this.loadAll();
      },
      error: () => {
        this.savingPayout = false;
        this.toast.error('Could not save payout details.');
      }
    });
  }

  submitCashOut(): void {
    if (this.cashOutForm.invalid || this.submitting) return;
    const v = this.cashOutForm.value;
    const gross = Number(v.grossAmount);
    const min = this.policy?.minimumCashOutAmountPkr ?? 500;
    if (gross < min) {
      this.toast.error(`Minimum cash-out is ${min} PKR.`);
      return;
    }
    this.submitting = true;
    this.walletService.createMyCashOutRequest({
      grossAmount: gross,
      method: v.method,
      notes: v.notes || null
    }).subscribe({
      next: () => {
        this.submitting = false;
        this.toast.success('Cash-out request submitted.');
        this.cashOutForm.patchValue({ grossAmount: null, notes: '' });
        this.preview = null;
        this.loadAll();
      },
      error: err => {
        this.submitting = false;
        const msg = err?.error?.message ?? 'Cash-out request failed.';
        this.toast.error(msg);
      }
    });
  }

  formatMoney(n: number | null | undefined): string {
    return `${(n ?? 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} PKR`;
  }
}
