import { inject } from '@angular/core';
import { CanMatchFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

/** Match public marketing routes only when the user is not signed in. */
export const guestCanMatch: CanMatchFn = () => !inject(AuthService).isLoggedIn();
