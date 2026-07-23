import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
import { PlatformService } from '../../../core/services/platform.service';
import {
  CompanyWorkspace,
  CreateWorkspaceDefinitionPayload,
  Tenant,
  UpdateWorkspaceDefinitionPayload,
  WorkspaceDefinition
} from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-workspace-management',
  templateUrl: './workspace-management.component.html',
  styleUrls: ['./workspace-management.component.scss']
})
export class WorkspaceManagementComponent implements OnInit, OnDestroy {
  loading = false;
  saving = false;
  selectedTenant: Tenant | null = null;
  companyWorkspaces: CompanyWorkspace[] = [];
  catalog: WorkspaceDefinition[] = [];
  categoryFilter = '';
  enabledFilter: 'all' | 'enabled' | 'disabled' = 'all';
  drawerOpen = false;
  editing: WorkspaceDefinition | null = null;
  creating = false;

  form;
  private originalEnabled = new Set<string>();
  private destroy$ = new Subject<void>();

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private toast: UiToastService,
    private tenantContext: PlatformTenantContextService,
    private platform: PlatformService
  ) {
    this.form = this.fb.group({
      workspaceKey: [''],
      displayName: ['', Validators.required],
      description: [''],
      category: [''],
      icon: ['workspaces'],
      homeRoute: ['/dashboard', Validators.required],
      sortOrder: [0, Validators.required],
      visible: [true],
      isActive: [true],
      isMobileSupported: [false],
      moduleKeys: [''],
      featureKey: [''],
      defaultDashboardKey: ['']
    });
  }

  ngOnInit(): void {
    const tenantIdParam = this.route.snapshot.queryParamMap.get('tenantId');
    if (tenantIdParam) {
      const id = parseInt(tenantIdParam, 10);
      if (Number.isFinite(id)) this.tenantContext.selectTenantById(id);
    }

    this.tenantContext.tenant$.pipe(takeUntil(this.destroy$)).subscribe(t => (this.selectedTenant = t));
    this.tenantContext.tenantId$.pipe(takeUntil(this.destroy$)).subscribe(id => {
      if (id) this.loadCompany(id);
      else this.companyWorkspaces = [];
    });
    this.loadCatalog();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get categories(): string[] {
    return [...new Set(this.companyWorkspaces.map(w => w.category || 'General'))].sort((a, b) =>
      a.localeCompare(b)
    );
  }

  get filtered(): CompanyWorkspace[] {
    let list = this.companyWorkspaces;
    if (this.categoryFilter) {
      list = list.filter(w => (w.category || 'General') === this.categoryFilter);
    }
    if (this.enabledFilter === 'enabled') list = list.filter(w => w.isEnabled);
    if (this.enabledFilter === 'disabled') list = list.filter(w => !w.isEnabled);
    return list;
  }

  workspacesInCategory(category: string): CompanyWorkspace[] {
    return this.filtered.filter(w => (w.category || 'General') === category);
  }

  private loadCatalog(): void {
    this.platform.getWorkspaceCatalog().subscribe({
      next: rows => (this.catalog = rows ?? []),
      error: () => (this.catalog = [])
    });
  }

  private loadCompany(tenantId: number): void {
    this.loading = true;
    this.platform.getCompanyWorkspaces(tenantId).subscribe({
      next: rows => {
        this.companyWorkspaces = (rows ?? []).map(r => ({ ...r }));
        this.originalEnabled = new Set(rows.filter(r => r.isEnabled).map(r => r.workspaceKey));
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load workspaces.'));
      }
    });
  }

  toggle(ws: CompanyWorkspace, enabled: boolean): void {
    if (!ws.canToggle || !this.selectedTenant) return;
    ws.isEnabled = enabled;
  }

  saveCompany(): void {
    if (!this.selectedTenant) return;
    this.saving = true;
    const keys = this.companyWorkspaces.filter(w => w.isEnabled).map(w => w.workspaceKey);
    this.platform.setCompanyWorkspaces(this.selectedTenant.id, keys).subscribe({
      next: () => {
        this.saving = false;
        this.originalEnabled = new Set(keys);
        this.toast.success('Company workspaces saved.');
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to save workspaces.'));
      }
    });
  }

  openCreate(): void {
    this.creating = true;
    this.editing = null;
    this.form.reset({
      workspaceKey: '',
      displayName: '',
      description: '',
      category: 'General',
      icon: 'workspaces',
      homeRoute: '/dashboard',
      sortOrder: 200,
      visible: true,
      isActive: true,
      isMobileSupported: false,
      moduleKeys: '',
      featureKey: '',
      defaultDashboardKey: ''
    });
    this.form.get('workspaceKey')?.enable();
    this.drawerOpen = true;
  }

  openEdit(ws: CompanyWorkspace): void {
    const def = this.catalog.find(c => c.workspaceKey === ws.workspaceKey);
    this.creating = false;
    this.editing = def ?? {
      workspaceKey: ws.workspaceKey,
      displayName: ws.displayName,
      description: ws.description,
      category: ws.category,
      icon: ws.icon,
      homeRoute: ws.homeRoute,
      sortOrder: 0,
      visible: true,
      isActive: true,
      isMobileSupported: ws.isMobileSupported,
      moduleKeys: ws.moduleKeys ?? [],
      featureKey: null,
      defaultDashboardKey: ws.defaultDashboardKey
    };
    this.form.patchValue({
      workspaceKey: this.editing.workspaceKey,
      displayName: this.editing.displayName,
      description: this.editing.description || '',
      category: this.editing.category || '',
      icon: this.editing.icon || 'workspaces',
      homeRoute: this.editing.homeRoute,
      sortOrder: this.editing.sortOrder,
      visible: this.editing.visible,
      isActive: this.editing.isActive,
      isMobileSupported: this.editing.isMobileSupported,
      moduleKeys: (this.editing.moduleKeys || []).join(', '),
      featureKey: this.editing.featureKey || '',
      defaultDashboardKey: this.editing.defaultDashboardKey || ''
    });
    this.form.get('workspaceKey')?.disable();
    this.drawerOpen = true;
  }

  closeDrawer(): void {
    this.drawerOpen = false;
    this.editing = null;
    this.creating = false;
  }

  saveDefinition(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const moduleKeys = String(raw.moduleKeys || '')
      .split(',')
      .map((s: string) => s.trim())
      .filter(Boolean);

    this.saving = true;
    if (this.creating) {
      const payload: CreateWorkspaceDefinitionPayload = {
        workspaceKey: String(raw.workspaceKey || '').trim(),
        displayName: raw.displayName || '',
        description: raw.description || null,
        category: raw.category || null,
        icon: raw.icon || null,
        homeRoute: raw.homeRoute || '/dashboard',
        sortOrder: Number(raw.sortOrder) || 0,
        visible: !!raw.visible,
        isMobileSupported: !!raw.isMobileSupported,
        moduleKeys,
        featureKey: raw.featureKey || null,
        defaultDashboardKey: raw.defaultDashboardKey || null
      };
      this.platform.createWorkspaceDefinition(payload).subscribe({
        next: () => {
          this.saving = false;
          this.toast.success('Workspace created.');
          this.closeDrawer();
          this.loadCatalog();
          if (this.selectedTenant) this.loadCompany(this.selectedTenant.id);
        },
        error: err => {
          this.saving = false;
          this.toast.error(apiErrorMessage(err, 'Failed to create workspace.'));
        }
      });
      return;
    }

    if (!this.editing) return;
    const payload: UpdateWorkspaceDefinitionPayload = {
      displayName: raw.displayName || '',
      description: raw.description || null,
      category: raw.category || null,
      icon: raw.icon || null,
      homeRoute: raw.homeRoute || '/dashboard',
      sortOrder: Number(raw.sortOrder) || 0,
      visible: !!raw.visible,
      isActive: !!raw.isActive,
      isMobileSupported: !!raw.isMobileSupported,
      moduleKeys,
      featureKey: raw.featureKey || null,
      defaultDashboardKey: raw.defaultDashboardKey || null
    };
    this.platform.updateWorkspaceDefinition(this.editing.workspaceKey, payload).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Workspace updated.');
        this.closeDrawer();
        this.loadCatalog();
        if (this.selectedTenant) this.loadCompany(this.selectedTenant.id);
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to update workspace.'));
      }
    });
  }

  deactivate(ws: CompanyWorkspace): void {
    if (!confirm(`Deactivate workspace "${ws.displayName}"?`)) return;
    this.platform.deactivateWorkspaceDefinition(ws.workspaceKey).subscribe({
      next: () => {
        this.toast.success('Workspace deactivated.');
        this.loadCatalog();
        if (this.selectedTenant) this.loadCompany(this.selectedTenant.id);
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to deactivate workspace.'))
    });
  }

  back(): void {
    void this.router.navigate(['/platform']);
  }
}
