import { ChangeDetectionStrategy, Component, OnInit, computed, inject, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AppBrandLoaderComponent } from '../../../shared/components/app-brand-loader/app-brand-loader.component';
import { DriverWizardFacade } from './services/driver-wizard.facade';
import { UiPageHeaderComponent } from '../../../shared/components/ui/page-header/ui-page-header.component';
import { WizardStepperComponent } from '../../vehicles/vehicle-register-wizard/components/wizard-stepper/wizard-stepper.component';
import { WizardFooterComponent } from '../../vehicles/vehicle-register-wizard/components/wizard-footer/wizard-footer.component';
import { WizardStepPersonalComponent } from './components/wizard-step-personal/wizard-step-personal.component';
import { WizardStepLicenseComponent } from './components/wizard-step-license/wizard-step-license.component';
import { WizardStepOrganizationComponent } from './components/wizard-step-organization/wizard-step-organization.component';
import { DriverWizardSidebarComponent } from './components/driver-wizard-sidebar/driver-wizard-sidebar.component';
import { DriverDocType } from './models/driver-wizard.model';
import { UiBreadcrumb } from '../../../shared/components/ui/types/ui.types';

@Component({
  selector: 'app-driver-register-wizard',
  standalone: true,
  imports: [
    MatIconModule,
    MatProgressSpinnerModule,
    AppBrandLoaderComponent,
    UiPageHeaderComponent,
    WizardStepperComponent,
    WizardFooterComponent,
    WizardStepPersonalComponent,
    WizardStepLicenseComponent,
    WizardStepOrganizationComponent,
    DriverWizardSidebarComponent
  ],
  providers: [DriverWizardFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './driver-register-wizard.component.html',
  styleUrls: ['./driver-register-wizard.component.scss']
})
export class DriverRegisterWizardComponent implements OnInit {
  readonly facade = inject(DriverWizardFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly breadcrumbs = computed<UiBreadcrumb[]>(() =>
    this.facade.isEditMode()
      ? [{ label: 'Drivers', route: '/drivers' }, { label: 'Edit Driver' }]
      : [{ label: 'Drivers', route: '/drivers' }, { label: 'New Registration' }]
  );

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(pm => {
      const id = pm.get('id');
      const isEditRoute = this.route.snapshot.url.some(s => s.path === 'edit');
      this.facade.init(isEditRoute && id ? +id : undefined);
    });
  }

  photoPreview(): string | undefined {
    return this.facade.resolvedPhotoPreview() ?? undefined;
  }

  phoneDisplay(): string {
    const v = this.facade.form.getRawValue();
    return `${v.phoneCountryCode ?? '+971'} ${v.phoneLocal ?? ''}`.trim();
  }

  branchLabel(): string {
    const branchId = this.facade.form.getRawValue().branchId;
    if (!branchId) return '—';
    return this.facade.branchOptions().find(b => b.value === branchId)?.label ?? '—';
  }

  onDocSelected(event: { type: DriverDocType; file: File | null }): void {
    this.facade.onDocSelected(event.type, event.file);
  }
}
