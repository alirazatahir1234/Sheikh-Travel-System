import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MaintenanceService } from '../../../../core/services/maintenance.service';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { Part, PartsInventoryStats } from '../../../../core/models/maintenance.model';
import { PartsInventoryStatsComponent } from './components/parts-inventory-stats.component';
import { PartsFiltersComponent, PartsFilterState } from './components/parts-filters.component';
import { PartsAlertsComponent } from './components/parts-alerts.component';
import { PartsTableComponent } from './components/parts-table.component';
import { PartFormDrawerComponent } from './components/part-form-drawer.component';
import { AddStockDrawerComponent } from './components/add-stock-drawer.component';
import { IssuePartDrawerComponent } from './components/issue-part-drawer.component';
import { TransferStockDrawerComponent } from './components/transfer-stock-drawer.component';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';

@Component({
  selector: 'app-parts-inventory-page',
  standalone: true,
  imports: [
    MatIconModule,
    PartsInventoryStatsComponent,
    PartsFiltersComponent,
    PartsAlertsComponent,
    PartsTableComponent,
    PartFormDrawerComponent,
    AddStockDrawerComponent,
    IssuePartDrawerComponent,
    TransferStockDrawerComponent
  ],
  templateUrl: './parts-inventory-page.component.html',
  styleUrls: ['./parts-inventory-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PartsInventoryPageComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly toast = inject(UiToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly stats = signal<PartsInventoryStats | null>(null);
  readonly parts = signal<Part[]>([]);
  readonly loading = signal(false);

  readonly draftFilters = signal<PartsFilterState>({ search: '' });
  readonly appliedSearch = signal('');

  readonly formDrawerOpen = signal(false);
  readonly addStockOpen = signal(false);
  readonly issueOpen = signal(false);
  readonly transferOpen = signal(false);
  readonly selectedPart = signal<Part | null>(null);

  ngOnInit(): void {
    if (this.route.snapshot.queryParamMap.get('create') === 'true') {
      this.formDrawerOpen.set(true);
    }
    this.loadAll();
  }

  onDraftChange(state: PartsFilterState): void {
    this.draftFilters.set(state);
  }

  onApply(): void {
    this.appliedSearch.set(this.draftFilters().search.trim());
    this.loadParts();
  }

  onReset(): void {
    this.draftFilters.set({ search: '' });
    this.appliedSearch.set('');
    this.loadParts();
  }

  openCreate(): void {
    this.formDrawerOpen.set(true);
  }

  openAddStock(part: Part): void {
    this.selectedPart.set(part);
    this.addStockOpen.set(true);
  }

  openIssue(part: Part): void {
    this.selectedPart.set(part);
    this.issueOpen.set(true);
  }

  openTransfer(part: Part): void {
    this.selectedPart.set(part);
    this.transferOpen.set(true);
  }

  onSaved(): void {
    this.loadAll();
  }

  private loadAll(): void {
    this.loadStats();
    this.loadParts();
  }

  private loadStats(): void {
    this.maintenanceService.getPartsInventoryStats()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: s => this.stats.set(s),
        error: err => this.toast.error(apiErrorMessage(err, 'Failed to load stats'))
      });
  }

  private loadParts(): void {
    this.loading.set(true);
    const search = this.appliedSearch() || undefined;
    this.maintenanceService.getParts(search)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: rows => { this.parts.set(rows); this.loading.set(false); },
        error: err => {
          this.loading.set(false);
          this.toast.error(apiErrorMessage(err, 'Failed to load parts'));
        }
      });
  }
}
