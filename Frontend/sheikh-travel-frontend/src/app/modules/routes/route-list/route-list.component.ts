import { Component, OnInit } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { RouteService } from '../../../core/services/route.service';
import { Route } from '../../../core/models/route.model';

@Component({
  selector: 'app-route-list',
  templateUrl: './route-list.component.html',
  styleUrls: ['./route-list.component.scss']
})
export class RouteListComponent implements OnInit {
  displayedColumns = ['name', 'origin', 'destination', 'distanceKm', 'estimatedMinutes', 'isActive', 'actions'];
  dataSource = new MatTableDataSource<Route>();
  loading = true;
  error: string | null = null;
  totalCount = 0;

  constructor(private routeService: RouteService, private router: Router, private snackBar: MatSnackBar) {}

  ngOnInit(): void { this.load(); }

  load(page = 1, pageSize = 10): void {
    this.loading = true;
    this.error = null;
    this.routeService.getAll(page, pageSize).subscribe({
      next: r => { this.dataSource.data = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; this.error = 'Failed to load routes.'; }
    });
  }

  edit(id: number): void { this.router.navigate(['/routes', id, 'edit']); }

  delete(id: number): void {
    if (!confirm('Delete this route?')) return;
    this.routeService.delete(id).subscribe({
      next: () => { this.snackBar.open('Deleted', 'Close', { duration: 2000 }); this.load(); },
      error: () => this.snackBar.open('Delete failed', 'Close', { duration: 3000 })
    });
  }

  applyFilter(event: Event): void {
    this.dataSource.filter = (event.target as HTMLInputElement).value.trim().toLowerCase();
  }
}
