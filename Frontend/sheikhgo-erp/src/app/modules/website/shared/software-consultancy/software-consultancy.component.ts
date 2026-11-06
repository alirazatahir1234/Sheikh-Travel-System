import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { SOFTWARE_CONSULTANCY_SERVICES } from '../../core/site-catalog';
import { WebsiteTranslatePipe } from '../website-translate.pipe';

@Component({
  selector: 'app-software-consultancy',
  standalone: true,
  imports: [RouterLink, MatIconModule, WebsiteTranslatePipe],
  templateUrl: './software-consultancy.component.html',
  styleUrl: './software-consultancy.component.scss',
})
export class SoftwareConsultancyComponent {
  readonly services = SOFTWARE_CONSULTANCY_SERVICES;
}
