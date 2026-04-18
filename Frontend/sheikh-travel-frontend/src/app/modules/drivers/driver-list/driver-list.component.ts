import { Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { DriverService } from '../../../core/services/driver.service';
import { Driver } from '../../../core/models/driver.model';

@Component({
  selector: 'app-driver-list',
  templateUrl: './driver-list.component.html',
  styleUrls: ['./driver-list.component.scss']
})
export class DriverListComponent implements OnInit {
  displayedColumns = ['fullName', 'phone', 'licenseNumber', 'licenseExpiryDate', 'isActive', 'actions'];
  dataSource = new MatTableDataSource<Driver>();
  loading = true;
  error: string | null = null;
  totalCount = 0;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(private driverService: DriverService, private router: Router, private snackBar: MatSnackBar) {}

  ngOnInit(): void { this.load(); }

  load(page = 1, pageSize = 10): void {
    this.loading = true;
    this.error = null;
    this.driverService.getAll(page, pageSize).subscribe({
      next: r => { this.dataSource.data = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; this.error = 'Failed to load drivers.'; }
    });
  }

  edit(id: number): void { this.router.navigate(['/drivers', id, 'edit']); }

  delete(id: number): void {
    if (!confirm('Delete this driver?')) return;
    this.driverService.delete(id).subscribe({
      next: () => { this.snackBar.open('Deleted', 'Close', { duration: 2000 }); this.load(); },
      error: () => this.snackBar.open('Delete failed', 'Close', { duration: 3000 })
    });
  }

  isLicenseExpiring(date: string): boolean {
    const diff = new Date(date).getTime() - Date.now();
    return diff > 0 && diff < 30 * 24 * 60 * 60 * 1000;
  }

  applyFilter(event: Event): void {
    this.dataSource.filter = (event.target as HTMLInputElement).value.trim().toLowerCase();
  }
}
