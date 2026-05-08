import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { LoginComponent } from './login/login.component';
import { LoginHeroComponent } from './login/components/login-hero/login-hero.component';
import { LoginFormCardComponent } from './login/components/login-form-card/login-form-card.component';

const routes: Routes = [{ path: 'login', component: LoginComponent }];

@NgModule({
  declarations: [LoginComponent, LoginHeroComponent, LoginFormCardComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class AuthModule {}
