import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CustomerSessionService } from '../services/customer-session.service';

export const portalAuthGuard: CanActivateFn = (_route, state) => {
  const session = inject(CustomerSessionService);
  const router = inject(Router);

  if (session.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/profile'], { queryParams: { returnUrl: state.url } });
};
