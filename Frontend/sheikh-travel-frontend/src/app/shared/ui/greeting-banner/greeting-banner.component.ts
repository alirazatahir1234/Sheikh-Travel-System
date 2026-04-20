import { Component, Input } from '@angular/core';

@Component({
  selector: 'stb-greeting-banner',
  templateUrl: './greeting-banner.component.html',
  styleUrls: ['./greeting-banner.component.scss']
})
export class GreetingBannerComponent {
  @Input() userName = 'there';
  @Input() notificationsCount = 0;
  @Input() subject = 'notifications';
  @Input() date: Date = new Date();

  get timeOfDay(): 'Morning' | 'Afternoon' | 'Evening' {
    const h = this.date.getHours();
    if (h < 12) return 'Morning';
    if (h < 17) return 'Afternoon';
    return 'Evening';
  }
}
