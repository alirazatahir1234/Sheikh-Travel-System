import { Component, OnInit, inject } from '@angular/core';
import { SettingsService } from '../services/settings.service';
import { SettingsCategory } from '../models/settings.model';
import { SETTINGS_CATEGORIES_FALLBACK } from '../config/settings-categories';

@Component({
  standalone: false,
  selector: 'app-settings-layout',
  templateUrl: './settings-layout.component.html',
  styleUrls: ['./settings-layout.component.scss']
})
export class SettingsLayoutComponent implements OnInit {
  private readonly settings = inject(SettingsService);

  /** Always start with local catalog so nav is never empty while API loads. */
  categories: SettingsCategory[] = [...SETTINGS_CATEGORIES_FALLBACK];

  ngOnInit(): void {
    this.settings.getCategories().subscribe({
      next: (categories) => {
        this.categories = categories?.length ? categories : [...SETTINGS_CATEGORIES_FALLBACK];
      },
      error: () => {
        this.categories = [...SETTINGS_CATEGORIES_FALLBACK];
      }
    });
  }
}
