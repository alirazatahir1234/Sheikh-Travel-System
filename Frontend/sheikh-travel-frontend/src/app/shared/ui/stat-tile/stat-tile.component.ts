import { Component, Input } from '@angular/core';
import { AccentColor } from '../ui.types';

@Component({
  selector: 'stb-stat-tile',
  templateUrl: './stat-tile.component.html',
  styleUrls: ['./stat-tile.component.scss']
})
export class StatTileComponent {
  @Input() label = '';
  @Input() value: string | number = 0;
  @Input() hint?: string;
  @Input() icon = 'insights';
  @Input() color: AccentColor = 'teal';
  @Input() prefix = '';
  @Input() suffix = '';
  @Input() loading = false;
}
