import { Component, EventEmitter, Input, Output } from '@angular/core';
import { QuickLaunchApp } from '../ui.types';

@Component({
  selector: 'stb-quick-launch-grid',
  templateUrl: './quick-launch-grid.component.html',
  styleUrls: ['./quick-launch-grid.component.scss']
})
export class QuickLaunchGridComponent {
  @Input() title = 'Quick access';
  @Input() apps: QuickLaunchApp[] = [];
  @Output() launch = new EventEmitter<QuickLaunchApp>();

  trackById(_i: number, a: QuickLaunchApp): string { return a.id; }
}
