import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { WebsiteDashboardComponent } from './pages/website-dashboard/website-dashboard.component';
import { WebsiteHomeEditorComponent } from './pages/website-home-editor/website-home-editor.component';
import { WebsiteFeaturesComponent } from './pages/website-features/website-features.component';
import { WebsitePagesComponent } from './pages/website-pages/website-pages.component';
import { WebsitePageEditorComponent } from './pages/website-page-editor/website-page-editor.component';
import { WebsiteContactRequestsComponent } from './pages/website-contact-requests/website-contact-requests.component';
import { WebsiteDemoRequestsComponent } from './pages/website-demo-requests/website-demo-requests.component';
import { WebsiteMediaComponent } from './pages/website-media/website-media.component';
import { WebsiteLegalComponent } from './pages/website-legal/website-legal.component';
import { WebsiteSettingsComponent } from './pages/website-settings/website-settings.component';

const routes: Routes = [
  { path: '', component: WebsiteDashboardComponent },
  { path: 'home', component: WebsiteHomeEditorComponent },
  { path: 'features', component: WebsiteFeaturesComponent },
  { path: 'pages', component: WebsitePagesComponent },
  { path: 'pages/:id', component: WebsitePageEditorComponent },
  { path: 'contact-requests', component: WebsiteContactRequestsComponent },
  { path: 'demo-requests', component: WebsiteDemoRequestsComponent },
  { path: 'media', component: WebsiteMediaComponent },
  { path: 'legal', component: WebsiteLegalComponent },
  { path: 'settings', component: WebsiteSettingsComponent }
];

@NgModule({
  declarations: [
    WebsiteDashboardComponent,
    WebsiteHomeEditorComponent,
    WebsiteFeaturesComponent,
    WebsitePagesComponent,
    WebsitePageEditorComponent,
    WebsiteContactRequestsComponent,
    WebsiteDemoRequestsComponent,
    WebsiteMediaComponent,
    WebsiteLegalComponent,
    WebsiteSettingsComponent
  ],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class WebsiteAdminModule {}
