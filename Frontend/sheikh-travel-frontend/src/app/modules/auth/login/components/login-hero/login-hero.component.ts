import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-login-hero',
  templateUrl: './login-hero.component.html',
  styleUrls: ['./login-hero.component.scss']
})
export class LoginHeroComponent {
  @Input() year = new Date().getFullYear();
}
