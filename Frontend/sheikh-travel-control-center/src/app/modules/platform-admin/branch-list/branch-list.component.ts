import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PlatformService } from '../../../core/services/platform.service';
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
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.platform.getBranches().subscribe({
      next: rows => { this.branches = rows; this.loading = false; },
      error: () => { this.loading = false; this.snackBar.open('Failed to load branches.', 'Close', { duration: 3000 }); }
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
      error: (err) => this.snackBar.open(apiErrorMessage(err, 'Delete failed.'), 'Close', { duration: 4000 })
    });
  }
}
