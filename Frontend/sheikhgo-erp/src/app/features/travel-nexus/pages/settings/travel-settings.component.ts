import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { UiPageHeaderComponent, UiTab, UiTabsComponent } from '../../../../shared/components/ui';
import { DocumentTypesTabComponent } from './document-types-tab.component';
import { ExchangeRatesTabComponent } from './exchange-rates-tab.component';
import { GeneralSettingsTabComponent } from './general-settings-tab.component';
import { HotelsTabComponent } from './hotels-tab.component';
import { LeadSourcesTabComponent } from './lead-sources-tab.component';
import { LostReasonsTabComponent } from './lost-reasons-tab.component';
import { VisaTypesTabComponent } from './visa-types-tab.component';

@Component({
  selector: 'app-travel-settings',
  standalone: true,
  imports: [UiPageHeaderComponent, UiTabsComponent, VisaTypesTabComponent, DocumentTypesTabComponent,
    LeadSourcesTabComponent, LostReasonsTabComponent, GeneralSettingsTabComponent, HotelsTabComponent, ExchangeRatesTabComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="Settings" eyebrow="TravelNexus" icon="tune"
        subtitle="Visa types per country, document types, lead sources, lost reasons, hotels, exchange rates and numbering."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Settings' }]"></ui-page-header>
      <ui-tabs [tabs]="tabs" [(activeId)]="active"></ui-tabs>
      @switch (active()) {
        @case ('visa-types') { <app-visa-types-tab /> }
        @case ('document-types') { <app-document-types-tab /> }
        @case ('lead-sources') { <app-lead-sources-tab /> }
        @case ('lost-reasons') { <app-lost-reasons-tab /> }
        @case ('hotels') { <app-hotels-tab /> }
        @case ('exchange-rates') { <app-exchange-rates-tab /> }
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
    { id: 'lost-reasons', label: 'Lost reasons', icon: 'block' },
    { id: 'hotels', label: 'Hotels', icon: 'hotel' },
    { id: 'exchange-rates', label: 'Exchange rates', icon: 'currency_exchange' },
    { id: 'general', label: 'General', icon: 'settings' }
  ];
  /** `?tab=exchange-rates` opens a tab directly (links from the profitability page). */
  protected readonly active = signal(this.initialTab());

  private initialTab(): string {
    const tab = inject(ActivatedRoute).snapshot.queryParamMap.get('tab');
    return this.tabs.some(t => t.id === tab) ? tab! : 'visa-types';
  }
}
