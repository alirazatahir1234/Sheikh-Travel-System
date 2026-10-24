import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, merge, of, Subject, switchMap, takeUntil, tap } from 'rxjs';
import { PlatformService } from '../../../core/services/platform.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import {
  Branch,
  BranchStatus,
  branchStatusLabel,
  normalizeBranch,
  normalizeBranchStatus
} from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-branch-list',
  templateUrl: './branch-list.component.html',
  styleUrls: ['./branch-list.component.scss']
})
export class BranchListComponent implements OnInit, OnDestroy {
  loading = true;
  branches: Branch[] = [];
  /** When set (e.g. from tenant detail), load/mutate that company's branches. */
  tenantId: number | null = null;
  readonly displayedColumns = ['code', 'name', 'type', 'city', 'manager', 'status', 'actions'];
  readonly branchStatusLabel = branchStatusLabel;
  readonly BranchStatus = BranchStatus;

  private readonly destroy$ = new Subject<void>();
  private readonly reload$ = new Subject<void>();

  constructor(
    private platform: PlatformService,
    private router: Router,
    private route: ActivatedRoute,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    const tenantFromRoute$ = this.route.queryParamMap.pipe(
      tap(params => {
        const raw = params.get('tenantId');
        const parsed = raw != null && raw !== '' ? Number(raw) : NaN;
        this.tenantId = Number.isFinite(parsed) && parsed > 0 ? parsed : null;
      })
    );

    merge(tenantFromRoute$, this.reload$).pipe(
      takeUntil(this.destroy$),
      switchMap(() => {
        this.loading = true;
        const source = this.tenantId != null
          ? this.platform.getBranchesForTenant(this.tenantId)
          : this.platform.getBranches();
        return source.pipe(
          catchError(err => {
            this.toast.error(apiErrorMessage(err, 'Failed to load branches.'));
            return of([] as Branch[]);
          })
        );
      })
    ).subscribe(rows => {
      this.branches = (rows ?? []).map(normalizeBranch);
      this.loading = false;
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  load(): void {
    this.reload$.next();
  }

  add(): void {
    void this.router.navigate(
      ['/platform/branches/new'],
      this.tenantId != null ? { queryParams: { tenantId: this.tenantId } } : undefined
    );
  }

  edit(branch: Branch): void {
    void this.router.navigate(
      ['/platform/branches', branch.id, 'edit'],
      this.tenantId != null ? { queryParams: { tenantId: this.tenantId } } : undefined
    );
  }

  statusClass(status: BranchStatus | string | number): string {
    switch (normalizeBranchStatus(status)) {
      case BranchStatus.Active: return 'ok';
      case BranchStatus.Inactive: return 'off';
      case BranchStatus.Maintenance: return 'maintenance';
      case BranchStatus.Closed: return 'closed';
      default: return 'off';
    }
  }

  remove(branch: Branch): void {
    if (!confirm(`Delete branch "${branch.name}"?`)) return;
    const req = this.tenantId != null
      ? this.platform.deleteBranchForTenant(this.tenantId, branch.id)
      : this.platform.deleteBranch(branch.id);

    req.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toast.success('Branch deleted.');
        this.load();
      },
      error: (err) => this.toast.error(apiErrorMessage(err, 'Delete failed.'))
    });
  }
}
