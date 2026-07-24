import { Component, OnInit } from '@angular/core';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { UiTableColumn } from '../../../shared/components/ui';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import {
  PermissionCoverageEndpoint,
  PermissionCoverageReport,
  PermissionCoverageService
} from '../../../core/services/permission-coverage.service';

@Component({
  standalone: false,
  selector: 'app-permission-coverage',
  templateUrl: './permission-coverage.component.html',
  styleUrls: ['./permission-coverage.component.scss']
})
export class PermissionCoverageComponent implements OnInit {
  loading = false;
  report: PermissionCoverageReport | null = null;
  endpoints: PermissionCoverageEndpoint[] = [];
  statusFilter = '';
  moduleFilter = '';

  readonly breadcrumbs = [
    { label: 'Platform', route: '/platform' },
    { label: 'Permission Coverage' }
  ];

  readonly statusOptions = [
    { value: '', label: 'All statuses' },
    { value: 'Protected', label: 'Protected' },
    { value: 'PartiallyProtected', label: 'Partially protected' },
    { value: 'Public', label: 'Public' },
    { value: 'Internal', label: 'Internal' }
  ];

  readonly columns: UiTableColumn<PermissionCoverageEndpoint>[] = [
    { key: 'module', label: 'Module', width: '110px' },
    { key: 'controller', label: 'Controller', width: '180px' },
    { key: 'httpMethod', label: 'Method', width: '80px' },
    { key: 'route', label: 'Endpoint' },
    { key: 'requiredPermission', label: 'Required Permission', width: '200px' },
    { key: 'coverageStatus', label: 'Status', width: '140px' }
  ];

  constructor(
    private coverageApi: PermissionCoverageService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  get moduleOptions(): { value: string; label: string }[] {
    const modules = [...new Set(this.endpoints.map(e => e.module))].sort((a, b) =>
      a.localeCompare(b)
    );
    return [{ value: '', label: 'All modules' }, ...modules.map(m => ({ value: m, label: m }))];
  }

  get filteredEndpoints(): PermissionCoverageEndpoint[] {
    return this.endpoints.filter(e => {
      if (this.statusFilter && e.coverageStatus !== this.statusFilter) return false;
      if (this.moduleFilter && e.module !== this.moduleFilter) return false;
      return true;
    });
  }

  load(): void {
    this.loading = true;
    this.coverageApi.getCoverage().subscribe({
      next: report => {
        this.report = report;
        this.endpoints = report.endpoints ?? [];
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load permission coverage.'));
      }
    });
  }

  statusClass(status: string): string {
    switch (status) {
      case 'Protected':
        return 'ok';
      case 'PartiallyProtected':
        return 'warn';
      case 'Public':
        return 'public';
      case 'Internal':
        return 'internal';
      default:
        return '';
    }
  }
}
