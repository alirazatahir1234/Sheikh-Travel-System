import { Component, Input } from '@angular/core';
import { APP_PRODUCT_NAME, APP_LOGO_PATH, COMPANY_NAME } from '../../../../../core/constants/app-brand';

@Component({
  selector: 'app-login-hero',
  templateUrl: './login-hero.component.html',
  styleUrls: ['./login-hero.component.scss']
})
export class LoginHeroComponent {
  @Input() year = new Date().getFullYear();
  readonly productName = APP_PRODUCT_NAME;
  readonly companyName = COMPANY_NAME;
  readonly logoPath = APP_LOGO_PATH;
}
