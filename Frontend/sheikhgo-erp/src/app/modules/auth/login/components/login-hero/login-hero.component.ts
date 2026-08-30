import { Component, Input } from '@angular/core';
import { APP_PRODUCT_NAME, COMPANY_NAME } from '../../../../../core/constants/app-brand';

@Component({
  standalone: false,
  selector: 'app-login-hero',
  templateUrl: './login-hero.component.html',
  styleUrls: ['./login-hero.component.scss']
})
export class LoginHeroComponent {
  @Input() year = new Date().getFullYear();
  readonly productName = APP_PRODUCT_NAME;
  readonly companyName = COMPANY_NAME;
}
