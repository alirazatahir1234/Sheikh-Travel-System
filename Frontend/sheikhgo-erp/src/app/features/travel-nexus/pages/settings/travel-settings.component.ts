import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { UiPageHeaderComponent, UiTab, UiTabsComponent } from '../../../../shared/components/ui';
import { DocumentTypesTabComponent } from './document-types-tab.component';
import { GeneralSettingsTabComponent } from './general-settings-tab.component';
import { LeadSourcesTabComponent } from './lead-sources-tab.component';
import { VisaTypesTabComponent } from './visa-types-tab.component';

@Component({
  selector: 'app-travel-settings',
  standalone: true,
  imports: [UiPageHeaderComponent, UiTabsComponent, VisaTypesTabComponent, DocumentTypesTabComponent,
    LeadSourcesTabComponent, GeneralSettingsTabComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="Settings" eyebrow="TravelNexus" icon="tune"
        subtitle="Visa types per country, document types, lead sources and numbering."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Settings' }]"></ui-page-header>
      <ui-tabs [tabs]="tabs" [(activeId)]="active"></ui-tabs>
      @switch (active()) {
        @case ('visa-types') { <app-visa-types-tab /> }
        @case ('document-types') { <app-document-types-tab /> }
        @case ('lead-sources') { <app-lead-sources-tab /> }
        @case ('general') { <app-general-settings-tab /> }
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelSettingsComponent {
  protected readonly tabs: UiTab[] = [
    { id: 'visa-types', label: 'Visa types', icon: 'flight' },
    { id: 'document-types', label: 'Document types', icon: 'description' },
    { id: 'lead-sources', label: 'Lead sources', icon: 'campaign' },
    { id: 'general', label: 'General', icon: 'settings' }
  ];
  protected readonly active = signal('visa-types');
}
