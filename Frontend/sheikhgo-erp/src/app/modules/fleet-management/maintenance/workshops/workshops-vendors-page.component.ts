import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { MaintenanceService } from '../../../../core/services/maintenance.service';
import { Workshop, Vendor, WorkshopVendorStats } from '../../../../core/models/maintenance.model';
import { WorkshopVendorStatsComponent } from './components/workshop-vendor-stats.component';
import { WorkshopTableComponent } from './components/workshop-table.component';
import { VendorTableComponent } from './components/vendor-table.component';
import { WorkshopFormDrawerComponent } from './components/workshop-form-drawer.component';
import { VendorFormDrawerComponent } from './components/vendor-form-drawer.component';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';

type TabId = 'workshops' | 'vendors';

@Component({
  selector: 'app-workshops-vendors-page',
  standalone: true,
  imports: [
    MatIconModule,
    WorkshopVendorStatsComponent,
    WorkshopTableComponent,
    VendorTableComponent,
    WorkshopFormDrawerComponent,
    VendorFormDrawerComponent
  ],
  templateUrl: './workshops-vendors-page.component.html',
  styleUrls: ['./workshops-vendors-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class WorkshopsVendorsPageComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly toast = inject(UiToastService);
  private readonly route = inject(ActivatedRoute);

  readonly stats = signal<WorkshopVendorStats | null>(null);
  readonly workshops = signal<Workshop[]>([]);
  readonly vendors = signal<Vendor[]>([]);
  readonly activeTab = signal<TabId>('workshops');
  readonly workshopDrawerOpen = signal(false);
  readonly vendorDrawerOpen = signal(false);
  readonly selectedWorkshop = signal<Workshop | null>(null);
  readonly selectedVendor = signal<Vendor | null>(null);

  ngOnInit(): void {
    const tab = this.route.snapshot.queryParamMap.get('tab');
    if (tab === 'vendors') this.activeTab.set('vendors');
    if (this.route.snapshot.queryParamMap.get('create') === 'true') {
      if (tab === 'vendors') this.openVendorCreate();
      else this.openWorkshopCreate();
    }
    this.loadAll();
  }

  selectTab(tab: TabId): void {
    this.activeTab.set(tab);
  }

  openWorkshopCreate(): void {
    this.selectedWorkshop.set(null);
    this.workshopDrawerOpen.set(true);
  }

  openVendorCreate(): void {
    this.selectedVendor.set(null);
    this.vendorDrawerOpen.set(true);
  }

  editWorkshop(w: Workshop): void {
    this.selectedWorkshop.set(w);
    this.workshopDrawerOpen.set(true);
  }

  editVendor(v: Vendor): void {
    this.selectedVendor.set(v);
    this.vendorDrawerOpen.set(true);
  }

  toggleWorkshop(w: Workshop, active: boolean): void {
    const req = active ? this.maintenanceService.activateWorkshop(w.id) : this.maintenanceService.deactivateWorkshop(w.id);
    req.subscribe({
      next: () => { this.loadAll(); this.toast.success(active ? 'Workshop activated' : 'Workshop deactivated'); },
      error: err => this.toast.error(apiErrorMessage(err, 'Action failed'))
    });
  }

  toggleVendor(v: Vendor, active: boolean): void {
    const req = active ? this.maintenanceService.activateVendor(v.id) : this.maintenanceService.deactivateVendor(v.id);
    req.subscribe({
      next: () => { this.loadAll(); this.toast.success(active ? 'Vendor activated' : 'Vendor deactivated'); },
      error: err => this.toast.error(apiErrorMessage(err, 'Action failed'))
    });
  }

  onSaved(): void {
    this.loadAll();
    this.toast.success('Saved successfully');
  }

  private loadAll(): void {
    this.maintenanceService.getWorkshopVendorStats().subscribe({
      next: s => this.stats.set(s),
      error: () => this.stats.set(null)
    });
    this.maintenanceService.getWorkshops().subscribe({
      next: w => this.workshops.set(w),
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to load workshops'))
    });
    this.maintenanceService.getVendors().subscribe({
      next: v => this.vendors.set(v),
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to load vendors'))
    });
  }
}
