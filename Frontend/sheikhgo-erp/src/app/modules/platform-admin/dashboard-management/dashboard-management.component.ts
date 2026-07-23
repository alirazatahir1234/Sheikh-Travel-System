import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { PlatformService } from '../../../core/services/platform.service';
import {
  DashboardDefinition,
  DashboardDetail,
  DashboardLayoutItem,
  DashboardWidgetDefinition,
  UpdateDashboardDefinitionPayload,
  UpdateDashboardLayoutItemPayload
} from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-dashboard-management',
  templateUrl: './dashboard-management.component.html',
  styleUrls: ['./dashboard-management.component.scss']
})
export class DashboardManagementComponent implements OnInit {
  loading = false;
  saving = false;
  catalog: DashboardDefinition[] = [];
  widgets: DashboardWidgetDefinition[] = [];
  selected: DashboardDetail | null = null;
  layoutDraft: DashboardLayoutItem[] = [];
  audienceFilter = '';
  statusFilter: 'all' | 'active' | 'inactive' = 'all';
  drawerOpen = false;
  editing: DashboardDefinition | null = null;

  form;

  constructor(
    private fb: FormBuilder,
    private router: Router,
    private toast: UiToastService,
    private platform: PlatformService
  ) {
    this.form = this.fb.group({
      displayName: ['', Validators.required],
      description: [''],
      audience: ['Both', Validators.required],
      defaultWorkspaceKey: [''],
      category: [''],
      sortOrder: [0, Validators.required],
      visible: [true],
      isActive: [true]
    });
  }

  ngOnInit(): void {
    this.loadCatalog();
    this.platform.getDashboardWidgets().subscribe({
      next: rows => (this.widgets = rows ?? []),
      error: () => (this.widgets = [])
    });
  }

  get filtered(): DashboardDefinition[] {
    let list = this.catalog;
    if (this.audienceFilter) {
      list = list.filter(d =>
        (d.audience || '').toLowerCase() === this.audienceFilter.toLowerCase()
      );
    }
    if (this.statusFilter === 'active') {
      list = list.filter(d => d.isActive && d.visible);
    }
    if (this.statusFilter === 'inactive') {
      list = list.filter(d => !d.isActive || !d.visible);
    }
    return list;
  }

  private loadCatalog(): void {
    this.loading = true;
    this.platform.getDashboardCatalog().subscribe({
      next: rows => {
        this.catalog = rows ?? [];
        this.loading = false;
        if (this.selected) {
          const key = this.selected.definition.dashboardKey;
          const still = this.catalog.find(c => c.dashboardKey === key);
          if (still) this.selectDashboard(still);
          else {
            this.selected = null;
            this.layoutDraft = [];
          }
        }
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load dashboards.'));
      }
    });
  }

  selectDashboard(def: DashboardDefinition): void {
    this.loading = true;
    this.platform.getDashboardByKey(def.dashboardKey).subscribe({
      next: detail => {
        this.selected = detail;
        this.layoutDraft = (detail.layout ?? []).map(i => ({ ...i }));
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load dashboard layout.'));
      }
    });
  }

  openEdit(def: DashboardDefinition): void {
    this.editing = def;
    this.form.patchValue({
      displayName: def.displayName,
      description: def.description || '',
      audience: def.audience || 'Both',
      defaultWorkspaceKey: def.defaultWorkspaceKey || '',
      category: def.category || '',
      sortOrder: def.sortOrder,
      visible: def.visible,
      isActive: def.isActive
    });
    this.drawerOpen = true;
  }

  closeDrawer(): void {
    this.drawerOpen = false;
    this.editing = null;
  }

  saveDefinition(): void {
    if (!this.editing || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const payload: UpdateDashboardDefinitionPayload = {
      displayName: raw.displayName || '',
      description: raw.description || null,
      audience: raw.audience || 'Both',
      defaultWorkspaceKey: raw.defaultWorkspaceKey || null,
      category: raw.category || null,
      sortOrder: Number(raw.sortOrder) || 0,
      visible: !!raw.visible,
      isActive: !!raw.isActive
    };
    this.saving = true;
    this.platform.updateDashboardDefinition(this.editing.dashboardKey, payload).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Dashboard updated.');
        this.closeDrawer();
        this.loadCatalog();
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to update dashboard.'));
      }
    });
  }

  move(item: DashboardLayoutItem, delta: number): void {
    const idx = this.layoutDraft.indexOf(item);
    const next = idx + delta;
    if (idx < 0 || next < 0 || next >= this.layoutDraft.length) return;
    const copy = [...this.layoutDraft];
    const [row] = copy.splice(idx, 1);
    copy.splice(next, 0, row);
    this.layoutDraft = copy.map((r, i) => ({ ...r, sortOrder: (i + 1) * 10 }));
  }

  toggleVisible(item: DashboardLayoutItem, visible: boolean): void {
    item.isVisible = visible;
  }

  addWidget(widgetKey: string): void {
    if (!widgetKey || this.layoutDraft.some(i => i.widgetKey === widgetKey)) return;
    const meta = this.widgets.find(w => w.widgetKey === widgetKey);
    this.layoutDraft = [
      ...this.layoutDraft,
      {
        widgetKey,
        displayName: meta?.displayName || widgetKey,
        category: meta?.category,
        icon: meta?.icon,
        sortOrder: (this.layoutDraft.length + 1) * 10,
        isVisible: true,
        permissionCode: meta?.permissionCode,
        featureKey: meta?.featureKey,
        moduleKey: meta?.moduleKey,
        supportsErp: meta?.supportsErp ?? true,
        supportsMobile: meta?.supportsMobile ?? true
      }
    ];
  }

  removeWidget(item: DashboardLayoutItem): void {
    this.layoutDraft = this.layoutDraft.filter(i => i !== item);
  }

  get availableWidgets(): DashboardWidgetDefinition[] {
    const used = new Set(this.layoutDraft.map(i => i.widgetKey));
    return this.widgets.filter(w => w.isActive && w.visible && !used.has(w.widgetKey));
  }

  saveLayout(): void {
    if (!this.selected) return;
    const items: UpdateDashboardLayoutItemPayload[] = this.layoutDraft.map((i, idx) => ({
      widgetKey: i.widgetKey,
      sortOrder: (idx + 1) * 10,
      isVisible: i.isVisible
    }));
    this.saving = true;
    this.platform
      .updateDashboardLayout(this.selected.definition.dashboardKey, { items })
      .subscribe({
        next: () => {
          this.saving = false;
          this.toast.success('Layout saved.');
          this.selectDashboard(this.selected!.definition);
          this.loadCatalog();
        },
        error: err => {
          this.saving = false;
          this.toast.error(apiErrorMessage(err, 'Failed to save layout.'));
        }
      });
  }

  back(): void {
    void this.router.navigate(['/platform']);
  }
}
