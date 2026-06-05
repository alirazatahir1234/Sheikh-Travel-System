import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'stb-promo-banner',
  templateUrl: './promo-banner.component.html',
  styleUrls: ['./promo-banner.component.scss']
})
export class PromoBannerComponent {
  @Input() title = '';
  @Input() subtitle = '';
  @Input() ctaLabel = 'Learn more';
  @Input() icon = 'rocket_launch';
  @Input() gradient: 'teal' | 'violet' | 'sunset' = 'teal';

  @Output() cta = new EventEmitter<void>();
}
