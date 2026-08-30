import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { SgLogoComponent } from '../../shared/components/logo/sg-logo.component';
import { LoginComponent } from './login/login.component';
import { LoginHeroComponent } from './login/components/login-hero/login-hero.component';
import { LoginFormCardComponent } from './login/components/login-form-card/login-form-card.component';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';

const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'forgot-password', component: ForgotPasswordComponent },
  { path: 'reset-password', component: ResetPasswordComponent }
];

@NgModule({
  declarations: [
    LoginComponent,
    LoginHeroComponent,
    LoginFormCardComponent,
    ForgotPasswordComponent,
    ResetPasswordComponent
  ],
  imports: [SharedModule, SgLogoComponent, RouterModule.forChild(routes)]
})
export class AuthModule {}
