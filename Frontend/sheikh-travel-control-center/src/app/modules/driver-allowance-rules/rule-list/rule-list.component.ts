import { Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { DriverAllowanceRuleService } from '../../../core/services/driver-allowance-rule.service';
import {
  DriverAllowanceRule,
  AllowanceCalculationType,
  AllowanceCalculationTypeLabels,
  AllowanceCalculationUnit
} from '../../../core/models/driver-allowance-rule.model';

@Component({
  selector: 'app-driver-allowance-rule-list',
  templateUrl: './rule-list.component.html',
  styleUrls: ['./rule-list.component.scss']
})
export class DriverAllowanceRuleListComponent implements OnInit {
  displayedColumns = [
    'priority', 'name', 'calculationType', 'value', 'distance',
    'routeFilter', 'isActive', 'actions'
  ];

  dataSource = new MatTableDataSource<DriverAllowanceRule>();
  loading = true;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private ruleService: DriverAllowanceRuleService,
    private router: Router,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.ruleService.getAll(1, 200).subscribe({
      next: result => {
        this.dataSource.data = result.items;
        setTimeout(() => (this.dataSource.paginator = this.paginator));
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snackBar.open('Failed to load allowance rules.', 'Close', { duration: 3000 });
      }
    });
  }

  edit(id: number): void { this.router.navigate(['/driver-allowance-rules', id, 'edit']); }

  delete(rule: DriverAllowanceRule): void {
    if (!confirm(`Delete rule "${rule.name}"?`)) return;
    this.ruleService.delete(rule.id).subscribe({
      next: () => { this.snackBar.open('Rule deleted.', 'Close', { duration: 2000 }); this.load(); },
      error: () => this.snackBar.open('Delete failed.', 'Close', { duration: 3000 })
    });
  }

  typeLabel(t: AllowanceCalculationType): string { return AllowanceCalculationTypeLabels[t] ?? 'Unknown'; }
  unit(t: AllowanceCalculationType):      string { return AllowanceCalculationUnit[t]          ?? ''; }

  distanceRange(r: DriverAllowanceRule): string {
    const min = r.minDistanceKm ?? null;
    const max = r.maxDistanceKm ?? null;
    if (min == null && max == null) return 'Any';
    if (min != null && max == null) return `≥ ${min} km`;
    if (min == null && max != null) return `≤ ${max} km`;
    return `${min}–${max} km`;
  }
}
