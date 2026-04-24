import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';

import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatIconModule } from '@angular/material/icon';
import { MatDialogModule } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatMenuModule } from '@angular/material/menu';
import { MatBadgeModule } from '@angular/material/badge';
import { MatListModule } from '@angular/material/list';
import { MatStepperModule } from '@angular/material/stepper';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatDividerModule } from '@angular/material/divider';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatRadioModule } from '@angular/material/radio';

import {
  InfoCardComponent,
  GreetingBannerComponent,
  QuickLaunchGridComponent,
  StatTileComponent,
  TaskListCardComponent,
  DataTableCardComponent,
  ArticleLinksCardComponent,
  PrayerTimesCardComponent,
  WeatherCardComponent,
  PromoBannerComponent
} from './ui';

const MATERIAL = [
  MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule,
  MatTableModule, MatPaginatorModule, MatSortModule, MatIconModule,
  MatDialogModule, MatSnackBarModule, MatSelectModule, MatProgressSpinnerModule,
  MatChipsModule, MatTooltipModule, MatDatepickerModule, MatNativeDateModule,
  MatMenuModule, MatBadgeModule, MatListModule, MatStepperModule,
  MatSidenavModule, MatDividerModule, MatSlideToggleModule, MatCheckboxModule,
  MatAutocompleteModule, MatRadioModule,
];

const UI = [
  InfoCardComponent,
  GreetingBannerComponent,
  QuickLaunchGridComponent,
  StatTileComponent,
  TaskListCardComponent,
  DataTableCardComponent,
  ArticleLinksCardComponent,
  PrayerTimesCardComponent,
  WeatherCardComponent,
  PromoBannerComponent,
];

@NgModule({
  declarations: [...UI],
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RouterModule, ...MATERIAL],
  exports: [
    CommonModule, ReactiveFormsModule, FormsModule, RouterModule,
    ...MATERIAL,
    ...UI,
  ]
})
export class SharedModule {}
