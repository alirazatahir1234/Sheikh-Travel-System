# Cloudflare free CDN — SheikhGo website media (Azure Blob)

**Status:** Worker deployed on Free plan. Apex/`www` DNS **not** switched. No Azure CDN.

| Item | Value |
|------|--------|
| Cloudflare account | `05cf981da1f681f3ee10789fd63ff04a` |
| Azure account | `sheikhtravelblob` (Central India) |
| Public containers (CDN) | `website-images`, `website-videos` |
| **Private (never CDN)** | `gps-playbacks`, `sheikhtravelcontainer`, backups |
| Proposed custom host (later) | `https://media.sheikhgo.com/...` |
| **Live CDN (workers.dev)** | `https://sheikhgo-media.sheikhgo.workers.dev` |

## Why a Worker (not plain CNAME)

Azure Blob requires `Host: sheikhtravelblob.blob.core.windows.net`. A proxied CNAME to the blob hostname with `Host: media.sheikhgo.com` fails unless Azure custom-domain + verification is completed. The Worker rewrites to the blob origin with the correct Host, allowlists containers, and sets long edge TTLs.

`gps-playbacks` stays **private + SAS only** (see [azure-blob-playback.md](./azure-blob-playback.md)). The Worker returns **403** for that path and any non-allowlisted container.

## URL shape (path-preserving)

| Asset | Origin (today) | CDN (workers.dev) |
|-------|----------------|-------------------|
| Image | `https://sheikhtravelblob.blob.core.windows.net/website-images/demo/….jpg` | `https://sheikhgo-media.sheikhgo.workers.dev/website-images/demo/….jpg` |
| Video | `https://sheikhtravelblob.blob.core.windows.net/website-videos/demo/….mp4` | `https://sheikhgo-media.sheikhgo.workers.dev/website-videos/demo/….mp4` |

## Cost vs Azure CDN

| | Cloudflare Free (this setup) | Azure CDN / Front Door |
|--|------------------------------|-------------------------|
| Cached edge hits | **$0** (Free plan Workers + Cache API / CDN cache) | Billable egress + CDN rules |
| Origin (Azure) egress | Charged **only on cache MISS** | Charged on miss + often higher CDN markup |
| HTTPS / TLS | Included (workers.dev + later custom host) | Included |
| Typical savings | Marketing images/videos re-hit from CF edge → **avoid repeat Azure egress** | Pay per GB delivered |

Workers Free: 100k requests/day class. Fine for marketing site traffic; upgrade only if you exceed.

## API token — rotate / never store in git

Agents and chat **cannot** create or revoke Cloudflare API tokens. Only Ali (account owner) can do that in the Cloudflare dashboard.

### Ali action (required after any chat-exposed token)

1. Open [Cloudflare → My Profile → API Tokens](https://dash.cloudflare.com/profile/api-tokens).
2. **Revoke** any token that was pasted into chat, committed, or shared (treat as compromised).
3. **Create Token** → template **Edit Cloudflare Workers**, or custom:
   - Account → Workers Scripts → Edit
   - Account → Account Settings → Read (optional)
   - Scope to account `05cf981da1f681f3ee10789fd63ff04a`
4. Copy the token **once**. Set it in the **shell only** for deploy — never write it into a file in this repo:

```bash
export CLOUDFLARE_API_TOKEN='…'   # paste once in this terminal session
export CLOUDFLARE_ACCOUNT_ID='05cf981da1f681f3ee10789fd63ff04a'
# Do NOT: echo into .env, .dev.vars committed to git, docs, scripts, or Cursor rules
unset CLOUDFLARE_API_TOKEN        # when finished
```

5. Prefer `wrangler login` (browser OAuth) when possible so no long-lived token sits in shell history.

**Repo policy:** no `cfut_…` values, no `CLOUDFLARE_API_TOKEN=` assignments in tracked files, no `.env` / `.dev.vars` with live CF credentials. `infra/cloudflare-media-cdn/.gitignore` already ignores `node_modules/`, `.wrangler/`, and `.dev.vars`.

## Deploy on Ali’s account (CLI — preferred)

```bash
export CLOUDFLARE_API_TOKEN='…'   # shell only; see token section above
export CLOUDFLARE_ACCOUNT_ID='05cf981da1f681f3ee10789fd63ff04a'
cd infra/cloudflare-media-cdn
npm install
npx wrangler deploy
```

Note the printed `workers.dev` URL. Current deployment:

**`https://sheikhgo-media.sheikhgo.workers.dev`**

Smoke tests:

```bash
CDN=https://sheikhgo-media.sheikhgo.workers.dev
curl -sI "$CDN/health"
curl -sI "$CDN/website-images/demo/sheikhgo-nexus-mobile-promo-poster.jpg"   # expect 200 + Cache-Control
curl -sI "$CDN/gps-playbacks/anything"                                      # expect 403
curl -sI "$CDN/sheikhtravelcontainer/anything"                              # expect 403
```

### Alternative: `wrangler login` (browser OAuth)

```bash
cd infra/cloudflare-media-cdn && npx wrangler login && npx wrangler deploy
```

Approve in the browser for account `05cf981da1f681f3ee10789fd63ff04a`.

## Deploy via Dashboard UI (no CLI)

1. Open [Workers & Pages](https://dash.cloudflare.com/05cf981da1f681f3ee10789fd63ff04a/workers-and-pages).
2. **Create** → **Create Worker** → name `sheikhgo-media` → Deploy stub.
3. **Edit code** → paste contents of `infra/cloudflare-media-cdn/src/index.js` → **Deploy**.
4. **Settings → Variables** → add:

| Variable | Value |
|----------|--------|
| `BLOB_ORIGIN` | `https://sheikhtravelblob.blob.core.windows.net` |
| `ALLOWED_CONTAINERS` | `website-images,website-videos` |
| `EDGE_TTL_IMAGES` | `2592000` |
| `EDGE_TTL_VIDEOS` | `2592000` |
| `BROWSER_TTL` | `604800` |

5. Copy the `*.workers.dev` URL for review.

## App media base (workers.dev — live; no registrar DNS)

Angular marketing base is the Worker origin (path-preserving). Set in `environment.ts`, `environment.prod.ts`, and `_website-media.scss` `$sg-website-images-base`:

```ts
// environment.ts / environment.prod.ts
websiteMediaBaseUrl: 'https://sheikhgo-media.sheikhgo.workers.dev'
```

`websiteAssetUrl()` / `websiteCssUrl()` map `/website/...` → `{base}/website-images|website-videos/...`.

Leave empty `websiteMediaBaseUrl` only for local `/website/` fallback during `ng serve` without CDN.  
**Do not** point `AzurePublicBaseUrl` / this CDN at `gps-playbacks`.  
**Do not** change `sheikhgo.com` / `www` DNS for this step.

## Cache rules (optional, after custom domain)

When `media.sheikhgo.com` is attached later (zone on Cloudflare):

- Cache Rule: hostname `media.sheikhgo.com` AND path starts with `/website-images/` or `/website-videos/` → Edge TTL **1 month** (or Override 30d), Browser TTL 7d, eligible for cache.
- WAF / Firewall: block path prefix `/gps-playbacks` (defense in depth; Worker already 403s).

## Custom hostname `media.sheikhgo.com` (NOT yet — review first)

Requires Cloudflare to control the zone **or** Workers custom domain on a CF zone. Today `sheikhgo.com` NS = Spaceship (`launch1/2.spaceship.net`); site on Vercel.

**Do not** change apex/`www` NS or any `sheikhgo.com` DNS until Ali approves.

Later options:

1. **Full zone move to Cloudflare** (affects apex/www — schedule cutover), then Workers → Custom Domains → `media.sheikhgo.com`.
2. Keep apex at Spaceship; only possible with CF partial CNAME (Business) or a separate CF-managed domain — not free/simple.

Until then, use `https://sheikhgo-media.sheikhgo.workers.dev` after switching `websiteMediaBaseUrl`.

## Checklist

- [x] Worker deployed — `https://sheikhgo-media.sheikhgo.workers.dev`
- [ ] Revoke any chat-exposed API token; new token shell-only (Ali)
- [ ] `GET /health` OK
- [ ] Image + video 200 via CDN
- [ ] `/gps-playbacks/*` → 403
- [ ] Azure container ACL: `gps-playbacks` still private
- [x] `websiteMediaBaseUrl` → workers.dev (app config; no DNS)
- [ ] Review before any `media.sheikhgo.com` DNS
- [ ] No Azure CDN profile created
- [ ] **No** `sheikhgo.com` DNS changes
