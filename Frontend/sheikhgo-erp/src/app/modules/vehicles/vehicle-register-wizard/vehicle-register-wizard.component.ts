import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { UiSelectOption } from '../../../shared/components/ui/types/ui.types';
import { AppBrandLoaderComponent } from '../../../shared/components/app-brand-loader/app-brand-loader.component';
import { VehicleWizardFacade } from './services/vehicle-wizard.facade';
import { WizardStepperComponent } from './components/wizard-stepper/wizard-stepper.component';
import { WizardSummaryPanelComponent } from './components/wizard-summary-panel/wizard-summary-panel.component';
import { WizardFooterComponent } from './components/wizard-footer/wizard-footer.component';
import { WizardStepDetailsComponent } from './components/steps/wizard-step-details/wizard-step-details.component';
import { WizardStepTechnicalComponent } from './components/steps/wizard-step-technical/wizard-step-technical.component';
import { WizardStepGpsComponent } from './components/steps/wizard-step-gps/wizard-step-gps.component';
import { WizardStepDocumentsComponent } from './components/steps/wizard-step-documents/wizard-step-documents.component';
import { WizardStepReviewComponent } from './components/steps/wizard-step-review/wizard-step-review.component';
import { resolveUploadUrl } from '../../../core/utils/upload-url.util';
import { CompanyScopeController } from '../../../shared/components/company-scope/company-scope.controller';
import { CompanyScopeGateComponent } from '../../../shared/components/company-scope/company-scope-gate.component';
import { CompanyScopeBannerComponent } from '../../../shared/components/company-scope/company-scope-banner.component';
import { Tenant } from '../../../core/models/platform.model';

@Component({
  selector: 'app-vehicle-register-wizard',
  standalone: true,
  imports: [
    MatProgressSpinnerModule,
    AppBrandLoaderComponent,
    WizardStepperComponent,
    WizardSummaryPanelComponent,
    WizardFooterComponent,
    WizardStepDetailsComponent,
    WizardStepTechnicalComponent,
    WizardStepGpsComponent,
    WizardStepDocumentsComponent,
    WizardStepReviewComponent,
    CompanyScopeGateComponent,
    CompanyScopeBannerComponent
  ],
  providers: [CompanyScopeController, VehicleWizardFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './vehicle-register-wizard.component.html',
  styleUrls: ['./vehicle-register-wizard.component.scss']
})
export class VehicleRegisterWizardComponent implements OnInit, OnDestroy {
  readonly facade = inject(VehicleWizardFacade);
  readonly companyScope = inject(CompanyScopeController);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly yearOptions = signal<UiSelectOption[]>(this.buildYearOptions());

  readonly previewImageUrl = computed(() => {
    const url = this.facade.primaryVehicleImageUrl();
    if (!url) return undefined;
    if (url.startsWith('blob:')) return url;
    return resolveUploadUrl(url) ?? undefined;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.companyScope.beginExisting();
      this.facade.init(+id);
      return;
    }
    // Super Admin picks the company first — the wizard creates a server draft on init.
    if (this.companyScope.beginCreate()) this.facade.init(undefined);
  }

  onCompanyConfirmed(tenant: Tenant): void {
    this.companyScope.confirm(tenant);
    if (this.facade.isEditMode()) {
      void this.facade.applyCompanyChange(tenant.id);
      return;
    }
    this.facade.init(undefined);
  }

  onCompanyGateCancelled(): void {
    if (this.facade.isEditMode()) {
      this.companyScope.cancelChange();
      return;
    }
    void this.router.navigate(['/vehicles']);
  }

  /**
   * Create: drop the empty draft, then pick again.
   * Edit: re-open the company gate and persist the new company on confirm.
   */
  async changeCompany(): Promise<void> {
    if (this.facade.isEditMode()) {
      this.companyScope.beginChange();
      return;
    }
    await this.facade.discardNewDraft();
    this.companyScope.restart('/vehicles/new');
  }

  ngOnDestroy(): void {
    this.facade.destroy();
  }

  onFileSelected(event: { index: number; file: File }): void {
    void this.facade.uploadDocumentSlot(event.index, event.file);
  }

  onVehicleImageSelected(event: { index: number; file: File }): void {
    void this.facade.uploadVehicleImage(event.index, event.file);
  }

  onVehicleImageRejected(event: { index: number; message: string }): void {
    this.facade.setVehicleImageSlotError(event.index, event.message);
  }

  onSelectPrimaryImage(index: number): void {
    void this.facade.selectPrimaryVehicleImage(index);
  }

  private buildYearOptions(): UiSelectOption[] {
    const current = new Date().getFullYear();
    const years: UiSelectOption[] = [];
    for (let y = current + 1; y >= current - 30; y--) {
      years.push({ value: String(y), label: String(y) });
    }
    return years;
  }
}
