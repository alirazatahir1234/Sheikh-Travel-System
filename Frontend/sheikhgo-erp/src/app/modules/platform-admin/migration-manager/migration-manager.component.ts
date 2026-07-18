import { Component, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { UiTableColumn } from '../../../shared/components/ui';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import {
  MigrationService,
  SchemaMigrationStatus
} from '../../../core/services/migration.service';

@Component({
  standalone: false,
  selector: 'app-migration-manager',
  templateUrl: './migration-manager.component.html',
  styleUrls: ['./migration-manager.component.scss'],
  providers: [DatePipe]
})
export class MigrationManagerComponent implements OnInit {
  loading = false;
  applying = false;
  migrations: SchemaMigrationStatus[] = [];

  readonly breadcrumbs = [
    { label: 'Platform', route: '/platform' },
    { label: 'Migration Manager' }
  ];

  readonly columns: UiTableColumn<SchemaMigrationStatus>[] = [
    { key: 'order', label: '#', width: '64px' },
    { key: 'name', label: 'Migration' },
    { key: 'isApplied', label: 'Status', width: '120px' },
    { key: 'appliedAtUtc', label: 'Applied On', width: '200px' },
    { key: 'appliedBy', label: 'Applied By', width: '180px' }
  ];

  constructor(
    private migrationsApi: MigrationService,
    private toast: UiToastService,
    private datePipe: DatePipe
  ) {}

  ngOnInit(): void {
    this.load();
  }

  get pendingCount(): number {
    return this.migrations.filter(m => !m.isApplied).length;
  }

  get appliedCount(): number {
    return this.migrations.filter(m => m.isApplied).length;
  }

  load(): void {
    this.loading = true;
    this.migrationsApi.getStatus().subscribe({
      next: rows => {
        this.migrations = rows;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load migrations.'));
      }
    });
  }

  applyPending(): void {
    if (this.applying || this.pendingCount === 0) {
      return;
    }

    this.applying = true;
    this.migrationsApi.applyPending().subscribe({
      next: result => {
        this.applying = false;
        if (result.failedMigration) {
          this.toast.error(
            `Failed on ${result.failedMigration}: ${result.errorMessage || 'Unknown error'}`
          );
        } else if (result.appliedCount === 0) {
          this.toast.info('No pending migrations to apply.');
        } else {
          this.toast.success(`Applied ${result.appliedCount} migration(s).`);
        }
        this.load();
      },
      error: err => {
        this.applying = false;
        this.toast.error(apiErrorMessage(err, 'Failed to apply pending migrations.'));
        this.load();
      }
    });
  }

  formatAppliedAt(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }
    return this.datePipe.transform(value, 'dd-MMM-yyyy HH:mm') || value;
  }
}
