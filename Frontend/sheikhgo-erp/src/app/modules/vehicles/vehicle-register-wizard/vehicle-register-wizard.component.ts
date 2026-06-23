import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';
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
    WizardStepReviewComponent
  ],
  providers: [VehicleWizardFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './vehicle-register-wizard.component.html',
  styleUrls: ['./vehicle-register-wizard.component.scss']
})
export class VehicleRegisterWizardComponent implements OnInit, OnDestroy {
  readonly facade = inject(VehicleWizardFacade);
  private readonly route = inject(ActivatedRoute);

  readonly yearOptions = signal<UiSelectOption[]>(this.buildYearOptions());

  readonly previewImageUrl = computed(() => {
    const url = this.facade.primaryVehicleImageUrl();
    if (!url) return undefined;
    if (url.startsWith('blob:')) return url;
    return resolveUploadUrl(url) ?? undefined;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.facade.init(id ? +id : undefined);
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
