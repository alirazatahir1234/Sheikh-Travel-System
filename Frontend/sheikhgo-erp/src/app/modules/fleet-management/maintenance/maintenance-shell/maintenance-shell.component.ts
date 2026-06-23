import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { debounceTime, distinctUntilChanged, Subject } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MaintenanceContextService } from '../maintenance-context.service';
import { MaintenanceService } from '../../../../core/services/maintenance.service';
import { MAINTENANCE_SUB_NAV } from '../maintenance-nav.config';
import { MaintenanceSearchResult } from '../../../../core/models/maintenance.model';
import { COMPANY_NAME } from '../../../../core/constants/app-brand';

@Component({
  selector: 'app-maintenance-shell',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterOutlet, RouterLink, RouterLinkActive, MatIconModule],
  templateUrl: './maintenance-shell.component.html',
  styleUrls: ['./maintenance-shell.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MaintenanceShellComponent {
  readonly companyName = COMPANY_NAME;
  private readonly ctx = inject(MaintenanceContextService);
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly router = inject(Router);
  private readonly searchInput$ = new Subject<string>();

  readonly subNav = MAINTENANCE_SUB_NAV;
  readonly period = this.ctx.period;
  readonly searchResults = signal<MaintenanceSearchResult[]>([]);
  readonly showSearchResults = signal(false);
  searchQuery = '';

  constructor() {
    this.searchInput$.pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(q => this.runSearch(q));
  }

  onSearchChange(value: string): void {
    this.searchQuery = value;
    this.ctx.searchTerm.set(value);
    this.searchInput$.next(value);
  }

  onPeriodChange(value: string): void {
    this.ctx.period.set(value);
  }

  exportReport(): void {
    this.ctx.requestExport();
  }

  navigateResult(r: MaintenanceSearchResult): void {
    this.showSearchResults.set(false);
    if (r.routeHint) this.router.navigateByUrl(r.routeHint);
  }

  private runSearch(q: string): void {
    if (!q || q.length < 2) {
      this.searchResults.set([]);
      this.showSearchResults.set(false);
      return;
    }
    this.maintenanceService.search(q).subscribe(results => {
      this.searchResults.set(results);
      this.showSearchResults.set(results.length > 0);
    });
  }
}
