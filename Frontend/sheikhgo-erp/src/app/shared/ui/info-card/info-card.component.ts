import { Component, Input } from '@angular/core';

/**
 * Generic rounded card with an optional header (title, subtitle, trailing actions slot)
 * and body slot. Use it to wrap any panel and keep visual language consistent.
 *
 * Example:
 *   <stb-info-card title="Files" subtitle="My files">
 *     <ng-container actions><button mat-button>View all</button></ng-container>
 *     ...body...
 *   </stb-info-card>
 */
@Component({
  selector: 'stb-info-card',
  templateUrl: './info-card.component.html',
  styleUrls: ['./info-card.component.scss']
})
export class InfoCardComponent {
  @Input() title?: string;
  @Input() subtitle?: string;
  @Input() icon?: string;
  @Input() padding: 'normal' | 'compact' | 'none' = 'normal';
  /** When true, the card has no shadow and blends into the page. */
  @Input() ghost = false;
}
