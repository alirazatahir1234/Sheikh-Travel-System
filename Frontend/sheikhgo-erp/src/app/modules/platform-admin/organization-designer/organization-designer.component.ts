import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { Subject, takeUntil } from 'rxjs';
import { PlatformTenantContextService } from '../../../core/services/platform-tenant-context.service';
import { PlatformService } from '../../../core/services/platform.service';
import { LookupService } from '../../../core/services/lookup.service';
import {
  OrganizationTree,
  OrganizationBranch,
  OrganizationDepartment,
  BranchPayload,
  DepartmentPayloadWithBranch,
  BranchStatus,
  BRANCH_TYPES,
  DEFAULT_CURRENCY
} from '../../../core/models/platform.model';
import { apiErrorMessage } from '../../../core/utils/api-error.util';

interface BranchNode {
  type: 'branch';
  data: OrganizationBranch;
  children: TreeNode[];
}

interface DepartmentNode {
  type: 'department';
  data: OrganizationDepartment;
}

type TreeNode = BranchNode | DepartmentNode;

interface SelectedBranch {
  type: 'branch';
  data: OrganizationBranch;
}

interface SelectedDepartment {
  type: 'department';
  data: OrganizationDepartment;
}

type SelectedNode = SelectedBranch | SelectedDepartment | null;

@Component({
  selector: 'app-organization-designer',
  templateUrl: './organization-designer.component.html',
  styleUrls: ['./organization-designer.component.scss']
})
export class OrganizationDesignerComponent implements OnInit, OnDestroy {
  loading = false;
  saving = false;
  organizationTree: OrganizationTree | null = null;
  treeNodes: TreeNode[] = [];
  selectedNode: SelectedNode = null;
  expandedBranches = new Set<number>();

  branchForm!: FormGroup;
  departmentForm!: FormGroup;
  showBranchForm = false;
  showDepartmentForm = false;
  editingBranchId: number | null = null;
  editingDepartmentId: number | null = null;

  readonly branchTypes = BRANCH_TYPES;
  countries: string[] = [];
  timezones: string[] = [];
  currencies: string[] = [];
  readonly BranchStatus = BranchStatus;

  private destroy$ = new Subject<void>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private fb: FormBuilder,
    private toast: UiToastService,
    private tenantContext: PlatformTenantContextService,
    private platform: PlatformService,
    private lookup: LookupService
  ) {
    this.initForms();
    this.lookup.getCountryNames().subscribe(c => this.countries = c);
    this.lookup.getCurrencyCodes().subscribe(c => this.currencies = c);
    this.lookup.getTimezoneIds().subscribe(t => this.timezones = t);
  }

  ngOnInit(): void {
    const tenantIdParam = this.route.snapshot.queryParamMap.get('tenantId');
    if (tenantIdParam) {
      const id = parseInt(tenantIdParam, 10);
      if (Number.isFinite(id)) {
        this.tenantContext.selectTenantById(id);
      }
    }

    this.tenantContext.tenantId$
      .pipe(takeUntil(this.destroy$))
      .subscribe(id => {
        if (id) {
          this.loadOrganizationTree(id);
        } else {
          this.organizationTree = null;
          this.treeNodes = [];
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initForms(): void {
    this.branchForm = this.fb.group({
      branchCode: ['', [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.required, Validators.maxLength(200)]],
      branchType: ['Office'],
      parentBranchId: [null],
      phone: [''],
      email: ['', Validators.email],
      address: [''],
      city: [''],
      country: ['Pakistan'],
      timeZone: ['Asia/Karachi'],
      currencyCode: [DEFAULT_CURRENCY],
      status: [BranchStatus.Active],
      isGpsEnabled: [true]
    });

    this.departmentForm = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      branchId: [null],
      departmentHeadUserId: [null]
    });
  }

  private loadOrganizationTree(tenantId: number): void {
    this.loading = true;
    this.platform.getOrganizationTree(tenantId).subscribe({
      next: (tree) => {
        this.organizationTree = tree;
        this.buildTreeNodes();
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.toast.error(apiErrorMessage(err, 'Failed to load organization tree.'));
      }
    });
  }

  private buildTreeNodes(): void {
    if (!this.organizationTree) {
      this.treeNodes = [];
      return;
    }

    const branches = this.organizationTree.branches;
    const branchMap = new Map<number, BranchNode>();

    branches.forEach(branch => {
      const node: BranchNode = {
        type: 'branch',
        data: branch,
        children: branch.departments.map(d => ({ type: 'department' as const, data: d }))
      };
      branchMap.set(branch.id, node);
    });

    const rootNodes: TreeNode[] = [];
    branches.forEach(branch => {
      const node = branchMap.get(branch.id)!;
      if (branch.parentBranchId && branchMap.has(branch.parentBranchId)) {
        branchMap.get(branch.parentBranchId)!.children.push(node);
      } else {
        rootNodes.push(node);
      }
    });

    this.treeNodes = rootNodes;
  }

  get selectedBranch(): OrganizationBranch | null {
    return this.selectedNode?.type === 'branch' ? this.selectedNode.data : null;
  }

  get selectedDepartment(): OrganizationDepartment | null {
    return this.selectedNode?.type === 'department' ? this.selectedNode.data : null;
  }

  selectNode(node: TreeNode): void {
    if (node.type === 'branch') {
      this.selectedNode = { type: 'branch', data: node.data };
    } else {
      this.selectedNode = { type: 'department', data: node.data };
    }
    this.showBranchForm = false;
    this.showDepartmentForm = false;
  }

  toggleBranch(branchId: number): void {
    if (this.expandedBranches.has(branchId)) {
      this.expandedBranches.delete(branchId);
    } else {
      this.expandedBranches.add(branchId);
    }
  }

  isBranchExpanded(branchId: number): boolean {
    return this.expandedBranches.has(branchId);
  }

  openAddBranch(parentBranchId?: number): void {
    this.editingBranchId = null;
    this.branchForm.reset({
      branchCode: '',
      name: '',
      branchType: 'Office',
      parentBranchId: parentBranchId ?? null,
      phone: '',
      email: '',
      address: '',
      city: '',
      country: 'Pakistan',
      timeZone: 'Asia/Karachi',
      currencyCode: DEFAULT_CURRENCY,
      status: BranchStatus.Active,
      isGpsEnabled: true
    });
    this.showBranchForm = true;
    this.showDepartmentForm = false;
    this.selectedNode = null;
  }

  openEditBranch(branch: OrganizationBranch): void {
    this.editingBranchId = branch.id;
    this.branchForm.patchValue({
      branchCode: branch.branchCode,
      name: branch.name,
      branchType: branch.branchType ?? 'Office',
      parentBranchId: branch.parentBranchId,
      city: branch.city ?? '',
      country: branch.country ?? 'Pakistan',
      status: branch.status
    });
    this.showBranchForm = true;
    this.showDepartmentForm = false;
  }

  openAddDepartment(branchId?: number): void {
    this.editingDepartmentId = null;
    this.departmentForm.reset({
      name: '',
      branchId: branchId ?? null,
      departmentHeadUserId: null
    });
    this.showDepartmentForm = true;
    this.showBranchForm = false;
    this.selectedNode = null;
  }

  openEditDepartment(dept: OrganizationDepartment): void {
    this.editingDepartmentId = dept.id;
    this.departmentForm.patchValue({
      name: dept.name,
      branchId: dept.branchId,
      departmentHeadUserId: null
    });
    this.showDepartmentForm = true;
    this.showBranchForm = false;
  }

  cancelForm(): void {
    this.showBranchForm = false;
    this.showDepartmentForm = false;
    this.editingBranchId = null;
    this.editingDepartmentId = null;
  }

  saveBranch(): void {
    if (!this.branchForm.valid || !this.organizationTree) return;

    const tenantId = this.organizationTree.tenantId;
    const values = this.branchForm.value;
    const payload: BranchPayload = {
      branchCode: values.branchCode,
      name: values.name,
      branchType: values.branchType,
      parentBranchId: values.parentBranchId,
      phone: values.phone,
      email: values.email,
      address: values.address,
      city: values.city,
      country: values.country,
      timeZone: values.timeZone,
      currencyCode: values.currencyCode,
      status: values.status,
      isGpsEnabled: values.isGpsEnabled
    };

    this.saving = true;

    if (this.editingBranchId) {
      this.platform.updateBranchForTenant(tenantId, this.editingBranchId, payload).subscribe({
        next: () => {
          this.toast.success('Branch updated.');
          this.saving = false;
          this.cancelForm();
          this.loadOrganizationTree(tenantId);
        },
        error: (err) => {
          this.saving = false;
          this.toast.error(apiErrorMessage(err, 'Failed to update branch.'));
        }
      });
    } else {
      this.platform.createBranchForTenant(tenantId, payload).subscribe({
        next: () => {
          this.toast.success('Branch created.');
          this.saving = false;
          this.cancelForm();
          this.loadOrganizationTree(tenantId);
        },
        error: (err) => {
          this.saving = false;
          this.toast.error(apiErrorMessage(err, 'Failed to create branch.'));
        }
      });
    }
  }

  saveDepartment(): void {
    if (!this.departmentForm.valid || !this.organizationTree) return;

    const tenantId = this.organizationTree.tenantId;
    const values = this.departmentForm.value;
    const payload: DepartmentPayloadWithBranch = {
      name: values.name,
      branchId: values.branchId,
      departmentHeadUserId: values.departmentHeadUserId
    };

    this.saving = true;

    if (this.editingDepartmentId) {
      this.platform.updateDepartmentForTenant(tenantId, this.editingDepartmentId, payload, true).subscribe({
        next: () => {
          this.toast.success('Department updated.');
          this.saving = false;
          this.cancelForm();
          this.loadOrganizationTree(tenantId);
        },
        error: (err) => {
          this.saving = false;
          this.toast.error(apiErrorMessage(err, 'Failed to update department.'));
        }
      });
    } else {
      this.platform.createDepartmentForTenant(tenantId, payload).subscribe({
        next: () => {
          this.toast.success('Department created.');
          this.saving = false;
          this.cancelForm();
          this.loadOrganizationTree(tenantId);
        },
        error: (err) => {
          this.saving = false;
          this.toast.error(apiErrorMessage(err, 'Failed to create department.'));
        }
      });
    }
  }

  deleteBranch(branch: OrganizationBranch): void {
    if (!this.organizationTree) return;
    if (!confirm(`Delete branch "${branch.name}"? Departments under this branch will become unassigned.`)) return;

    const tenantId = this.organizationTree.tenantId;
    this.platform.deleteBranchForTenant(tenantId, branch.id).subscribe({
      next: () => {
        this.toast.success('Branch deleted.');
        this.selectedNode = null;
        this.loadOrganizationTree(tenantId);
      },
      error: (err) => {
        this.toast.error(apiErrorMessage(err, 'Failed to delete branch.'));
      }
    });
  }

  deleteDepartment(dept: OrganizationDepartment): void {
    if (!this.organizationTree) return;
    if (!confirm(`Delete department "${dept.name}"?`)) return;

    const tenantId = this.organizationTree.tenantId;
    this.platform.deleteDepartmentForTenant(tenantId, dept.id).subscribe({
      next: () => {
        this.toast.success('Department deleted.');
        this.selectedNode = null;
        this.loadOrganizationTree(tenantId);
      },
      error: (err) => {
        this.toast.error(apiErrorMessage(err, 'Failed to delete department.'));
      }
    });
  }

  moveDepartment(dept: OrganizationDepartment, newBranchId: number | null): void {
    if (!this.organizationTree) return;

    const tenantId = this.organizationTree.tenantId;
    this.platform.moveDepartment(tenantId, dept.id, newBranchId).subscribe({
      next: () => {
        this.toast.success('Department moved.');
        this.loadOrganizationTree(tenantId);
      },
      error: (err) => {
        this.toast.error(apiErrorMessage(err, 'Failed to move department.'));
      }
    });
  }

  exportHierarchy(): void {
    if (!this.organizationTree) return;

    const data = {
      tenant: this.organizationTree.tenantName,
      branches: this.organizationTree.branches.map(b => ({
        code: b.branchCode,
        name: b.name,
        type: b.branchType,
        city: b.city,
        country: b.country,
        departments: b.departments.map(d => ({ name: d.name, staffCount: d.staffCount }))
      })),
      unassignedDepartments: this.organizationTree.unassignedDepartments.map(d => ({
        name: d.name,
        staffCount: d.staffCount
      }))
    };

    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${this.organizationTree.tenantName.replace(/\s+/g, '-').toLowerCase()}-hierarchy.json`;
    a.click();
    URL.revokeObjectURL(url);
  }

  goBack(): void {
    this.router.navigate(['/platform/tenants']);
  }

  getBranchesForParentSelect(): OrganizationBranch[] {
    return this.organizationTree?.branches.filter(b => b.id !== this.editingBranchId) ?? [];
  }
}
