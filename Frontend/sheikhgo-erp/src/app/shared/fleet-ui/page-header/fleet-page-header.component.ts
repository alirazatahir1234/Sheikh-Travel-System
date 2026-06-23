import { Component, Input } from '@angular/core';

@Component({
  selector: 'fleet-page-header',
  template: `
    <ui-page-header [title]="title" [subtitle]="subtitle" [eyebrow]="eyebrow" [icon]="icon">
      <ng-content></ng-content>
    </ui-page-header>
  `
})
export class FleetPageHeaderComponent {
  @Input() title = '';
  @Input() subtitle?: string;
  @Input() eyebrow?: string;
  @Input() icon?: string;
}
