import { APP_INITIALIZER, NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { LayoutModule } from './layout/layout.module';
import { AuthInterceptor } from './core/interceptors/auth.interceptor';
import { ApiEnvelopeInterceptor } from './core/interceptors/api-envelope.interceptor';
import { AppSplashComponent } from './shared/components/app-splash/app-splash.component';
import { AppConfigService } from './core/services/app-config.service';

@NgModule({
  declarations: [AppComponent],
  imports: [
    BrowserModule,
    BrowserAnimationsModule,
    HttpClientModule,
    AppRoutingModule,
    LayoutModule,
    AppSplashComponent
  ],
  providers: [
    // Runs before any other service is created: loads assets/config.json (API host, auth mode).
    {
      provide: APP_INITIALIZER,
      multi: true,
      deps: [AppConfigService],
      useFactory: (config: AppConfigService) => () => config.load()
    },
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: ApiEnvelopeInterceptor, multi: true }
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
