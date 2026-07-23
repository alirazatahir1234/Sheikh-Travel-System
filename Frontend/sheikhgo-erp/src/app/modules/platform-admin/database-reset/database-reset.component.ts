import { Component, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import {
  AdminSystemService,
  ResetDatabaseResult
} from '../../../core/services/admin-system.service';
import { ResetDatabaseDialogComponent } from './reset-database-dialog.component';

@Component({
  standalone: false,
  selector: 'app-database-reset',
  templateUrl: './database-reset.component.html',
  styleUrls: ['./database-reset.component.scss']
})
export class DatabaseResetComponent implements OnInit {
  loading = true;
  resetting = false;
  available = false;
  environmentName = '';
  lastResult: ResetDatabaseResult | null = null;

  readonly breadcrumbs = [
    { label: 'Platform', route: '/platform' },
    { label: 'Maintenance' }
  ];

  constructor(
    private adminSystem: AdminSystemService,
    private dialog: MatDialog,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.loadAvailability();
  }

  loadAvailability(): void {
    this.loading = true;
    this.adminSystem.getResetAvailability().subscribe({
      next: res => {
        this.available = res.available;
        this.environmentName = res.environment ?? '';
        this.loading = false;
      },
      error: () => {
        this.available = false;
        this.loading = false;
      }
    });
  }

  openResetDialog(): void {
    if (!this.available || this.resetting) return;

    const ref = this.dialog.open(ResetDatabaseDialogComponent, {
      width: '560px',
      maxWidth: '95vw',
      disableClose: true
    });

    ref.afterClosed().subscribe(confirmation => {
      if (confirmation !== 'RESET') return;
      this.executeReset();
    });
  }

  private executeReset(): void {
    this.resetting = true;
    this.adminSystem.resetDatabase('RESET').subscribe({
      next: result => {
        this.resetting = false;
        this.lastResult = result;
        this.toast.success(result.message || 'Database successfully reset.');
      },
      error: err => {
        this.resetting = false;
        this.toast.error(apiErrorMessage(err, 'Database reset failed.'));
      }
    });
  }
}
