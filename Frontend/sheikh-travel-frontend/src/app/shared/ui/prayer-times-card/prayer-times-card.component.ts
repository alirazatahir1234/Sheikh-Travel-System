import { Component, Input } from '@angular/core';
import { PrayerTime } from '../ui.types';

@Component({
  selector: 'stb-prayer-times-card',
  templateUrl: './prayer-times-card.component.html',
  styleUrls: ['./prayer-times-card.component.scss']
})
export class PrayerTimesCardComponent {
  @Input() title = 'Prayer Times';
  @Input() city = 'Sharjah';
  @Input() upcomingName = 'Dhuhr';
  @Input() upcomingTime = '12:21 PM';
  @Input() times: PrayerTime[] = [];
  @Input() compact = false;
}
