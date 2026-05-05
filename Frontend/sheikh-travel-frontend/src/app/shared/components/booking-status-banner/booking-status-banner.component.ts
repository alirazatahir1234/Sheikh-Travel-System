import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { BookingStatus } from '../../../core/models/booking.model';
import { BookingDetailBannerVm, getBookingDetailBanner } from '../../utils/booking-ledger.util';

@Component({
  selector: 'stb-booking-status-banner',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './booking-status-banner.component.html',
  styleUrls: ['./booking-status-banner.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BookingStatusBannerComponent {
  @Input({ required: true }) status!: BookingStatus;
  @Input({ required: true }) totalPaid!: number;
  @Input({ required: true }) totalAmount!: number;

  get vm(): BookingDetailBannerVm {
    return getBookingDetailBanner(this.status, this.totalPaid, this.totalAmount);
  }
}
