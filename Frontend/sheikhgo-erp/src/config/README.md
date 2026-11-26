# Runtime config

`config.json` is copied to `assets/config.json` at build time (`development/` for
`ng serve` and dev builds, `production/` for `--configuration production`). The app
reads it at startup through `AppConfigService` (an `APP_INITIALIZER`), so the API host
is not baked into the JavaScript bundle (audit SG-09).

| Key | Meaning |
| --- | --- |
| `apiUrl` | API base URL. `/api` means same origin (dev proxy, Docker/nginx). |
| `authMode` | `bearer` (tokens in the browser) or `cookie` (HttpOnly cookies, SG-54). |

`cookie` only takes effect when the API is on the same site as the ERP
(for example `www.sheikhgo.com` and `api.sheikhgo.com`). Otherwise the app falls
back to `bearer`, so a wrong value here cannot lock users out.

## Moving production to api.sheikhgo.com

1. Add a DNS A record: `api.sheikhgo.com` → the API VM. Caddy already serves that name.
2. In `production/config.json` set `"apiUrl": "https://api.sheikhgo.com/api"` and
   `"authMode": "cookie"`.
3. Redeploy the ERP. No code change is needed.
