# SheikhGo public website (inside ERP)

Marketing pages live in this module and are registered in `app-routing.module.ts`.

| Audience | URL |
|----------|-----|
| Guests | `/` Home, `/fleet-management`, `/gps-tracking`, `/features`, `/about`, `/contact`, `/request-demo` |
| Everyone | `/privacy-policy`, `/terms-and-conditions`, `/cookie-policy` |
| Auth | `/auth/login`, `/auth/forgot-password`, `/auth/reset-password` |

Signed-in users hitting `/` are redirected to `/dashboard`. Signed-in users get the ERP `/gps-tracking` module; guests get the marketing GPS page.
