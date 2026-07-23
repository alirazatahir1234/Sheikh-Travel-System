import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { PlatformService } from '../../../core/services/platform.service';
import {
  CreateMenuItemPayload,
  MenuCatalogItem,
  MenuCatalogModule,
  Permission,
  UpdateMenuItemPayload,
  UpdateMenuModulePayload
} from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

@Component({
  standalone: false,
  selector: 'app-menu-management',
  templateUrl: './menu-management.component.html',
  styleUrls: ['./menu-management.component.scss']
})
export class MenuManagementComponent implements OnInit, OnDestroy {
  loading = false;
  saving = false;
  modules: MenuCatalogModule[] = [];
  permissions: Permission[] = [];
  expanded = new Set<number>();
  categoryFilter = '';
  moduleFilter = '';
  activeFilter: 'all' | 'active' | 'inactive' = 'all';
  visibleFilter: 'all' | 'visible' | 'hidden' = 'all';
  permissionFilter: 'all' | 'with' | 'without' = 'all';

  editingModule: MenuCatalogModule | null = null;
  editingItem: MenuCatalogItem | null = null;
  creatingUnder: MenuCatalogModule | null = null;
  drawerOpen = false;

  moduleForm;
  itemForm;

  private destroy$ = new Subject<void>();

  constructor(
    private fb: FormBuilder,
    private router: Router,
    private toast: UiToastService,
    private platform: PlatformService
  ) {
    this.moduleForm = this.fb.group({
      displayName: ['', Validators.required],
      icon: [''],
      sortOrder: [0, Validators.required],
      visible: [true],
      isCollapsible: [true]
    });

    this.itemForm = this.fb.group({
      name: [''],
      displayName: ['', Validators.required],
      description: [''],
      category: [''],
      route: [''],
      icon: [''],
      permissionCode: [''],
      sortOrder: [0, Validators.required],
      isActive: [true],
      visible: [true],
      featureKey: [''],
      moduleKey: [''],
      isMobileSupported: [false]
    });
  }

  ngOnInit(): void {
    this.loadCatalog();
    this.platform.getPermissions().pipe(takeUntil(this.destroy$)).subscribe({
      next: perms => (this.permissions = perms ?? []),
      error: () => (this.permissions = [])
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get categories(): string[] {
    const set = new Set<string>();
    for (const m of this.modules) {
      for (const item of m.items) {
        if (item.category) set.add(item.category);
      }
    }
    return [...set].sort((a, b) => a.localeCompare(b));
  }

  get filteredModules(): MenuCatalogModule[] {
    return this.modules
      .filter(m => !this.moduleFilter || m.moduleKey === this.moduleFilter || String(m.id) === this.moduleFilter)
      .map(m => ({
        ...m,
        items: m.items.filter(item => this.itemMatches(item))
      }))
      .filter(m => m.items.length > 0 || (!this.categoryFilter && this.activeFilter === 'all' && this.visibleFilter === 'all' && this.permissionFilter === 'all'));
  }

  goBack(): void {
    void this.router.navigate(['/platform']);
  }

  refresh(): void {
    this.loadCatalog();
  }

  toggleExpand(id: number): void {
    if (this.expanded.has(id)) this.expanded.delete(id);
    else this.expanded.add(id);
  }

  isExpanded(id: number): boolean {
    return this.expanded.has(id);
  }

  openEditModule(module: MenuCatalogModule): void {
    this.editingModule = module;
    this.editingItem = null;
    this.creatingUnder = null;
    this.moduleForm.reset({
      displayName: module.displayName || module.name,
      icon: module.icon || '',
      sortOrder: module.sortOrder,
      visible: module.visible,
      isCollapsible: module.isCollapsible
    });
    this.drawerOpen = true;
  }

  openEditItem(item: MenuCatalogItem): void {
    this.editingItem = item;
    this.editingModule = null;
    this.creatingUnder = null;
    this.itemForm.reset({
      name: item.name,
      displayName: item.displayName || item.name,
      description: item.description || '',
      category: item.category || '',
      route: item.route || '',
      icon: item.icon || '',
      permissionCode: item.permissionCode || '',
      sortOrder: item.sortOrder,
      isActive: item.isActive,
      visible: item.visible,
      featureKey: item.featureKey || '',
      moduleKey: item.moduleKey || '',
      isMobileSupported: item.isMobileSupported
    });
    this.drawerOpen = true;
  }

  openCreateItem(module: MenuCatalogModule): void {
    this.creatingUnder = module;
    this.editingItem = null;
    this.editingModule = null;
    this.itemForm.reset({
      name: '',
      displayName: '',
      description: '',
      category: '',
      route: '',
      icon: 'circle',
      permissionCode: '',
      sortOrder: (module.items.length + 1) * 10,
      isActive: true,
      visible: true,
      featureKey: '',
      moduleKey: '',
      isMobileSupported: false
    });
    this.drawerOpen = true;
  }

  closeDrawer(): void {
    this.drawerOpen = false;
    this.editingModule = null;
    this.editingItem = null;
    this.creatingUnder = null;
  }

  saveDrawer(): void {
    if (this.editingModule) {
      this.saveModule();
      return;
    }
    if (this.creatingUnder) {
      this.saveCreate();
      return;
    }
    if (this.editingItem) {
      this.saveItem();
    }
  }

  deactivateItem(item: MenuCatalogItem): void {
    if (!confirm(`Deactivate "${item.displayName || item.name}"? It will be hidden from navigation.`)) return;
    this.saving = true;
    this.platform.deactivateMenuItem(item.id).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Menu item deactivated.');
        this.loadCatalog();
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to deactivate menu item.'));
      }
    });
  }

  private itemMatches(item: MenuCatalogItem): boolean {
    if (this.categoryFilter && (item.category || '') !== this.categoryFilter) return false;
    if (this.activeFilter === 'active' && !item.isActive) return false;
    if (this.activeFilter === 'inactive' && item.isActive) return false;
    if (this.visibleFilter === 'visible' && !item.visible) return false;
    if (this.visibleFilter === 'hidden' && item.visible) return false;
    if (this.permissionFilter === 'with' && !item.permissionCode) return false;
    if (this.permissionFilter === 'without' && item.permissionCode) return false;
    return true;
  }

  private loadCatalog(): void {
    this.loading = true;
    this.platform.getMenuCatalog().subscribe({
      next: catalog => {
        this.modules = catalog?.modules ?? [];
        if (this.expanded.size === 0) {
          for (const m of this.modules.slice(0, 3)) this.expanded.add(m.id);
        }
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load menu catalog.'));
      }
    });
  }

  private saveModule(): void {
    if (!this.editingModule || this.moduleForm.invalid) return;
    const v = this.moduleForm.getRawValue();
    const payload: UpdateMenuModulePayload = {
      displayName: v.displayName,
      icon: v.icon || null,
      sortOrder: Number(v.sortOrder) || 0,
      visible: !!v.visible,
      isCollapsible: !!v.isCollapsible
    };
    this.saving = true;
    this.platform.updateMenuModule(this.editingModule.id, payload).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Menu module updated.');
        this.closeDrawer();
        this.loadCatalog();
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to update module.'));
      }
    });
  }

  private saveItem(): void {
    if (!this.editingItem || this.itemForm.invalid) return;
    const v = this.itemForm.getRawValue();
    const payload: UpdateMenuItemPayload = {
      displayName: v.displayName,
      description: v.description || null,
      category: v.category || null,
      route: v.route || null,
      icon: v.icon || null,
      permissionCode: v.permissionCode || null,
      sortOrder: Number(v.sortOrder) || 0,
      isActive: !!v.isActive,
      visible: !!v.visible,
      featureKey: v.featureKey || null,
      moduleKey: v.moduleKey || null,
      isMobileSupported: !!v.isMobileSupported
    };
    this.saving = true;
    this.platform.updateMenuItem(this.editingItem.id, payload).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Menu item updated.');
        this.closeDrawer();
        this.loadCatalog();
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to update menu item.'));
      }
    });
  }

  private saveCreate(): void {
    if (!this.creatingUnder || this.itemForm.invalid) return;
    const v = this.itemForm.getRawValue();
    const name = (v.name || v.displayName || 'New Menu').trim();
    const payload: CreateMenuItemPayload = {
      moduleId: this.creatingUnder.id,
      name,
      displayName: v.displayName || name,
      description: v.description || null,
      category: v.category || null,
      route: v.route || null,
      icon: v.icon || null,
      permissionCode: v.permissionCode || null,
      sortOrder: Number(v.sortOrder) || 0,
      visible: v.visible !== false,
      featureKey: v.featureKey || null,
      moduleKey: v.moduleKey || null,
      isMobileSupported: !!v.isMobileSupported
    };
    this.saving = true;
    this.platform.createMenuItem(payload).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Menu item created.');
        this.closeDrawer();
        this.loadCatalog();
      },
      error: err => {
        this.saving = false;
        this.toast.error(apiErrorMessage(err, 'Failed to create menu item.'));
      }
    });
  }
}
