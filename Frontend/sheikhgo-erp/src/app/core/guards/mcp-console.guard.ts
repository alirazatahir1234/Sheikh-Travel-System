import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { isMcpConsoleEnabled } from '../navigation/mcp-console-access.util';
import { AuthService } from '../services/auth.service';

/**
 * SG-22 — block /mcp when environment.enableMcpConsole is false (production).
 */
export const mcpConsoleGuard: CanActivateFn = () => {
  if (isMcpConsoleEnabled()) {
    return true;
  }

  const auth = inject(AuthService);
  const router = inject(Router);
  return router.createUrlTree([auth.isLoggedIn() ? auth.getHomeRoute() : '/auth/login']);
};
