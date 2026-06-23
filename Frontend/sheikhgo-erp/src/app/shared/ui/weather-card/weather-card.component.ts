import { Component, Input } from '@angular/core';
import { WeatherInfo } from '../ui.types';

@Component({
  selector: 'stb-weather-card',
  templateUrl: './weather-card.component.html',
  styleUrls: ['./weather-card.component.scss']
})
export class WeatherCardComponent {
  @Input() info: WeatherInfo = {
    city: 'Pakistan',
    temperatureC: 32,
    condition: 'Partly cloudy',
    dateLabel: undefined
  };

  get iconName(): string {
    if (this.info.icon) return this.info.icon;
    const c = (this.info.condition || '').toLowerCase();
    if (c.includes('rain'))  return 'umbrella';
    if (c.includes('cloud')) return 'cloud';
    if (c.includes('storm')) return 'thunderstorm';
    if (c.includes('snow'))  return 'ac_unit';
    return 'wb_sunny';
  }
}
