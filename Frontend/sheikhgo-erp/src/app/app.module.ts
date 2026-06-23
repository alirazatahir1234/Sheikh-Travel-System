import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { LayoutModule } from './layout/layout.module';
import { AuthInterceptor } from './core/interceptors/auth.interceptor';
import { ApiEnvelopeInterceptor } from './core/interceptors/api-envelope.interceptor';
import { AppSplashComponent } from './shared/components/app-splash/app-splash.component';

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
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: ApiEnvelopeInterceptor, multi: true }
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
