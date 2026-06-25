import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Observable, Subject, combineLatest, of } from 'rxjs';
import { catchError, switchMap, takeUntil } from 'rxjs/operators';
import { SettingsService } from '../services/settings.service';
import { SETTINGS_SCHEMAS } from '../config/settings-schemas';
import { SettingFieldSchema, SettingsCategory, SettingsValues } from '../models/settings.model';
import { SettingsFormStatus } from '../components/dynamic-settings-form/dynamic-settings-form.component';

@Component({
  selector: 'app-settings-page',
  templateUrl: './settings-page.component.html',
  styleUrls: ['./settings-page.component.scss']
})
export class SettingsPageComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly settings = inject(SettingsService);
  private readonly destroy$ = new Subject<void>();

  category: SettingsCategory | null = null;
  schema: SettingFieldSchema[] = [];
  values: SettingsValues = {};
  loading = true;
  formStatus: SettingsFormStatus = { invalid: false, pristine: true, saving: false };

  onFormStatusChange(status: SettingsFormStatus): void {
    this.formStatus = status;
  }

  ngOnInit(): void {
    combineLatest([this.route.paramMap, this.settings.getCategories()])
      .pipe(
        takeUntil(this.destroy$),
        switchMap(([params, categories]): Observable<SettingsValues> => {
          const id = params.get('category') ?? 'General';
          this.category = categories.find(c => c.id.toLowerCase() === id.toLowerCase()) ?? null;
          this.schema = SETTINGS_SCHEMAS[this.category?.id ?? ''] ?? [];
          this.loading = true;

          if (!this.category?.isImplemented) {
            return of({});
          }
          return this.settings.getValues(this.category.id).pipe(catchError(() => of({})));
        })
      )
      .subscribe((values) => {
        this.values = values;
        this.loading = false;
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
