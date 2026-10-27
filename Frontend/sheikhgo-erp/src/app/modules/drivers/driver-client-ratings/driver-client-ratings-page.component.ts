import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { TripClientRating, TripFeedbackService } from '../../../core/services/trip-feedback.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { AuthService } from '../../../core/services/auth.service';
import { SharedModule } from '../../../shared/shared.module';

@Component({
  selector: 'app-driver-client-ratings-page',
  standalone: true,
  imports: [
    SharedModule,
    FormsModule,
    RouterLink,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
    MatTooltipModule,
  ],
  templateUrl: './driver-client-ratings-page.component.html',
  styleUrl: './driver-client-ratings-page.component.scss',
})
export class DriverClientRatingsPageComponent implements OnInit {
  private readonly feedback = inject(TripFeedbackService);
  private readonly toast = inject(UiToastService);
  private readonly auth = inject(AuthService);

  loading = true;
  rows: TripClientRating[] = [];
  total = 0;
  page = 1;
  pageSize = 25;
  driverIdFilter: number | null = null;
  includeHidden = false;

  get canManage(): boolean {
    return this.auth.hasPermission('Driver.Manage');
  }
  displayedColumns = ['trip', 'driver', 'scores', 'source', 'date', 'actions'];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.feedback.list(this.page, this.pageSize, this.driverIdFilter ?? undefined, this.includeHidden).subscribe({
      next: res => {
        this.rows = res.items;
        this.total = res.totalCount;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.toast.error('Failed to load client ratings.');
      },
    });
  }

  toggleHidden(row: TripClientRating): void {
    if (!this.canManage) return;
    this.feedback.setVisibility(row.id, !row.isHidden).subscribe({
      next: () => {
        row.isHidden = !row.isHidden;
        this.toast.success(row.isHidden ? 'Rating hidden from public aggregate.' : 'Rating visible again.');
      },
      error: () => this.toast.error('Could not update rating visibility.'),
    });
  }

  avgStars(row: TripClientRating): string {
    const p = row.politeness ?? row.score;
    const o = row.onTimeDelivery ?? row.score;
    const v = row.vehicleCondition ?? row.score;
    return ((p + o + v) / 3).toFixed(1);
  }
}
