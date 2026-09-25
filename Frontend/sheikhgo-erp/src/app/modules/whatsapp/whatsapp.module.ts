import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { permissionGuard } from '../../core/guards/permission.guard';
import { UiPageHeaderComponent } from '../../shared/components/ui/page-header/ui-page-header.component';
import { WhatsAppInboxComponent } from './whatsapp-inbox/whatsapp-inbox.component';
import { WhatsAppAccountsComponent } from './whatsapp-accounts/whatsapp-accounts.component';
import { WhatsAppAccountViewDialogComponent } from './whatsapp-accounts/whatsapp-account-view-dialog.component';
import { WhatsAppAccountTestDialogComponent } from './whatsapp-accounts/whatsapp-account-test-dialog.component';
import { WhatsAppTemplatesComponent } from './whatsapp-templates/whatsapp-templates.component';

import { WhatsAppWebhookLogsComponent } from './whatsapp-webhook-logs/whatsapp-webhook-logs.component';
import { WhatsAppAutomationsComponent } from './whatsapp-automations/whatsapp-automations.component';

const routes: Routes = [
  { path: '', component: WhatsAppInboxComponent },
  {
    path: 'accounts',
    canActivate: [permissionGuard],
    data: { permissions: ['WhatsApp.ManageAccounts', 'WhatsApp.Manage'] },
    component: WhatsAppAccountsComponent
  },
  {
    path: 'templates',
    canActivate: [permissionGuard],
    data: { permissions: ['WhatsApp.ManageTemplates', 'WhatsApp.View'] },
    component: WhatsAppTemplatesComponent
  },
  {
    path: 'automations',
    canActivate: [permissionGuard],
    data: { permissions: ['WhatsApp.Manage', 'WhatsApp.View'] },
    component: WhatsAppAutomationsComponent
  },
  {
    path: 'webhook-logs',
    canActivate: [permissionGuard],
    data: { permissions: ['WhatsApp.Manage'] },
    component: WhatsAppWebhookLogsComponent
  }
];

@NgModule({
  declarations: [
    WhatsAppInboxComponent,
    WhatsAppAccountsComponent,
    WhatsAppAccountViewDialogComponent,
    WhatsAppAccountTestDialogComponent,
    WhatsAppTemplatesComponent,
    WhatsAppAutomationsComponent,
    WhatsAppWebhookLogsComponent
  ],
  imports: [SharedModule, RouterModule.forChild(routes), UiPageHeaderComponent]
})
export class WhatsAppModule {}
