import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { PlatformService } from '../../../core/services/platform.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { Branch, BranchStatus, branchStatusLabel } from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  selector: 'app-branch-list',
  templateUrl: './branch-list.component.html',
  styleUrls: ['./branch-list.component.scss']
})
export class BranchListComponent implements OnInit {
  loading = true;
  branches: Branch[] = [];
  readonly branchStatusLabel = branchStatusLabel;
  readonly BranchStatus = BranchStatus;

  constructor(
    private platform: PlatformService,
    private router: Router,
    private toast: UiToastService
  ) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.platform.getBranches().subscribe({
      next: rows => { this.branches = rows; this.loading = false; },
      error: () => { this.loading = false; this.toast.error('Failed to load branches.'); }
    });
  }

  add(): void {
    void this.router.navigate(['/platform/branches/new']);
  }

  edit(branch: Branch): void {
    void this.router.navigate(['/platform/branches', branch.id, 'edit']);
  }

  statusClass(status: BranchStatus): string {
    switch (status) {
      case BranchStatus.Active: return 'ok';
      case BranchStatus.Inactive: return 'off';
      case BranchStatus.Maintenance: return 'maintenance';
      case BranchStatus.Closed: return 'closed';
      default: return 'off';
    }
  }

  remove(branch: Branch): void {
    if (!confirm(`Delete branch "${branch.name}"?`)) return;
    this.platform.deleteBranch(branch.id).subscribe({
      next: () => this.load(),
      error: (err) => this.toast.error(apiErrorMessage(err, 'Delete failed.'))
    });
  }
}
