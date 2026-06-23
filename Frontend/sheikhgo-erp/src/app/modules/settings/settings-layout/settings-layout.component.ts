import { Component, OnInit, inject } from '@angular/core';
import { SettingsService } from '../services/settings.service';
import { SettingsCategory } from '../models/settings.model';

@Component({
  selector: 'app-settings-layout',
  templateUrl: './settings-layout.component.html',
  styleUrls: ['./settings-layout.component.scss']
})
export class SettingsLayoutComponent implements OnInit {
  private readonly settings = inject(SettingsService);

  categories: SettingsCategory[] = [];
  loading = true;

  ngOnInit(): void {
    this.settings.getCategories().subscribe({
      next: (categories) => {
        this.categories = categories;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });
  }
}
