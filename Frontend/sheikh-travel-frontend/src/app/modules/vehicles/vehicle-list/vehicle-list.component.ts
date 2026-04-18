import { Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { VehicleService } from '../../../core/services/vehicle.service';
import { Vehicle } from '../../../core/models/vehicle.model';

@Component({
  selector: 'app-vehicle-list',
  templateUrl: './vehicle-list.component.html',
  styleUrls: ['./vehicle-list.component.scss']
})
export class VehicleListComponent implements OnInit {
  displayedColumns = ['name', 'registrationNumber', 'seatingCapacity', 'fuelAverage', 'isActive', 'actions'];
  dataSource = new MatTableDataSource<Vehicle>();
  loading = true;
  error: string | null = null;
  totalCount = 0;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private vehicleService: VehicleService,
    private router: Router,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void { this.load(); }

  load(page = 1, pageSize = 10): void {
    this.loading = true;
    this.error = null;
    this.vehicleService.getAll(page, pageSize).subscribe({
      next: result => {
        this.dataSource.data = result.items;
        this.totalCount = result.totalCount;
        this.loading = false;
      },
      error: () => { this.loading = false; this.error = 'Failed to load vehicles.'; }
    });
  }

  edit(id: number): void { this.router.navigate(['/vehicles', id, 'edit']); }

  delete(id: number): void {
    if (!confirm('Delete this vehicle?')) return;
    this.vehicleService.delete(id).subscribe({
      next: () => { this.snackBar.open('Deleted', 'Close', { duration: 2000 }); this.load(); },
      error: () => this.snackBar.open('Delete failed', 'Close', { duration: 3000 })
    });
  }

  toggleStatus(id: number): void {
    this.vehicleService.toggleStatus(id).subscribe(() => this.load());
  }

  applyFilter(event: Event): void {
    this.dataSource.filter = (event.target as HTMLInputElement).value.trim().toLowerCase();
  }
}
