/**
 * SheikhGo media CDN — proxies public Azure Blob containers only.
 * Allowlist: website-images, website-videos
 * Blocked: gps-playbacks and every other container/path
 *
 * Hotlink protection (Worker-level Referer/Origin):
 * - Allowed hosts: sheikhgo.com (+ subdomains), localhost / 127.0.0.1, *.vercel.app,
 *   and this Worker host (workers.dev).
 * - Empty Referer + empty Origin: ALLOWED (direct links, native apps, privacy
 *   browsers that strip Referer). Documented trade-off vs hard-block scrapers
 *   that omit Referer — container allowlist still limits what exists publicly.
 * - Present but non-allowlisted Referer or Origin → 403.
 * Cloudflare zone "Scrape Shield → Hotlink Protection" does not apply to
 * workers.dev; this Worker check is the gate.
 */
const DEFAULT_ORIGIN = "https://sheikhtravelblob.blob.core.windows.net";
const DEFAULT_ALLOWED = ["website-images", "website-videos"];
const BLOCKED = new Set([
  "gps-playbacks",
  "sheikhtravelcontainer",
  "sheikhgodbbkup",
  "$web",
]);

export default {
  async fetch(request, env, ctx) {
    if (request.method === "OPTIONS") {
      return corsPreflight(request);
    }

    if (request.method !== "GET" && request.method !== "HEAD") {
      return new Response("Method Not Allowed", { status: 405, headers: securityHeaders() });
    }

    const url = new URL(request.url);
    if (url.pathname === "/" || url.pathname === "/health") {
      return Response.json(
        {
          service: "sheikhgo-media",
          ok: true,
          allowed: [...parseAllowed(env)],
          origin: env.BLOB_ORIGIN || DEFAULT_ORIGIN,
          hotlink: {
            emptyReferrerAllowed: allowEmptyReferrer(env),
            allowedHosts:
              "sheikhgo.com, *.sheikhgo.com, localhost, 127.0.0.1, *.vercel.app, this Worker host",
          },
        },
        { headers: securityHeaders() },
      );
    }

    if (!isHotlinkAllowed(request, env)) {
      return new Response("Hotlink forbidden", {
        status: 403,
        headers: {
          ...securityHeaders(),
          "X-SheikhGo-Hotlink": "blocked",
        },
      });
    }

    const segments = url.pathname.split("/").filter(Boolean);
    const container = segments[0] ? decodeURIComponent(segments[0]) : "";

    if (!container || BLOCKED.has(container) || container.startsWith("$")) {
      return new Response("Forbidden", { status: 403, headers: securityHeaders() });
    }

    const allowed = parseAllowed(env);
    if (!allowed.has(container)) {
      return new Response("Forbidden", { status: 403, headers: securityHeaders() });
    }

    // Never forward SAS/query secrets into a public edge cache key for private containers —
    // this Worker never serves private containers; strip unused query for cache stability.
    const originBase = (env.BLOB_ORIGIN || DEFAULT_ORIGIN).replace(/\/$/, "");
    const originPath = "/" + segments.map(encodeURIComponent).join("/");
    const originUrl = originBase + originPath;

    const cache = caches.default;
    const cacheUrl = new URL(url.toString());
    cacheUrl.search = ""; // ignore query for public marketing assets
    const cacheKey = new Request(cacheUrl.toString(), { method: "GET" });

    if (request.method === "GET" && !request.headers.get("Range")) {
      const hit = await cache.match(cacheKey);
      if (hit) {
        const cached = new Response(hit.body, hit);
        cached.headers.set("X-SheikhGo-Cache", "HIT");
        return withCors(cached, request);
      }
    }

    const upstreamHeaders = new Headers();
    const range = request.headers.get("Range");
    if (range) upstreamHeaders.set("Range", range);
    const ifNoneMatch = request.headers.get("If-None-Match");
    if (ifNoneMatch) upstreamHeaders.set("If-None-Match", ifNoneMatch);
    const ifModifiedSince = request.headers.get("If-Modified-Since");
    if (ifModifiedSince) upstreamHeaders.set("If-Modified-Since", ifModifiedSince);

    const upstream = await fetch(originUrl, {
      method: request.method,
      headers: upstreamHeaders,
      redirect: "follow",
      cf: {
        cacheEverything: true,
        cacheTtlByStatus: {
          "200-299": Number(env.EDGE_TTL_VIDEOS || env.EDGE_TTL_IMAGES || 2592000),
          "404": 60,
          "400-499": 0,
          "500-599": 0,
        },
      },
    });

    if (upstream.status === 404) {
      return withCors(
        new Response("Not Found", { status: 404, headers: securityHeaders() }),
        request,
      );
    }

    if (!(upstream.ok || upstream.status === 206 || upstream.status === 304)) {
      return withCors(
        new Response("Upstream error", { status: 502, headers: securityHeaders() }),
        request,
      );
    }

    const headers = new Headers(upstream.headers);
    const isVideo = container === "website-videos";
    const edgeTtl = Number(
      (isVideo ? env.EDGE_TTL_VIDEOS : env.EDGE_TTL_IMAGES) || 2592000,
    );
    const browserTtl = Number(env.BROWSER_TTL || 604800);

    headers.set(
      "Cache-Control",
      `public, max-age=${browserTtl}, s-maxage=${edgeTtl}, immutable`,
    );
    headers.set("CDN-Cache-Control", `public, max-age=${edgeTtl}`);
    headers.set("Cloudflare-CDN-Cache-Control", `public, max-age=${edgeTtl}`);
    headers.set("X-Content-Type-Options", "nosniff");
    headers.set("X-SheikhGo-Cache", "MISS");
    headers.set("X-SheikhGo-Container", container);
    headers.delete("Set-Cookie");
    headers.delete("x-ms-request-id");
    headers.delete("x-ms-version");
    headers.delete("x-ms-lease-status");

    const response = new Response(upstream.body, {
      status: upstream.status,
      statusText: upstream.statusText,
      headers,
    });

    if (
      request.method === "GET" &&
      !range &&
      (upstream.status === 200 || upstream.status === 203)
    ) {
      ctx.waitUntil(cache.put(cacheKey, response.clone()));
    }

    return withCors(response, request);
  },
};

function parseAllowed(env) {
  const raw = env.ALLOWED_CONTAINERS || DEFAULT_ALLOWED.join(",");
  return new Set(
    raw
      .split(",")
      .map((s) => s.trim())
      .filter(Boolean),
  );
}

function securityHeaders() {
  return {
    "X-Content-Type-Options": "nosniff",
    "Cache-Control": "no-store",
  };
}

/**
 * Empty Referer/Origin allowed by default so direct opens, mobile/WebView,
 * and privacy clients still work. Set HOTLINK_ALLOW_EMPTY_REFERRER=false to
 * require an allowlisted Referer or Origin on every media request.
 */
function allowEmptyReferrer(env) {
  const v = (env.HOTLINK_ALLOW_EMPTY_REFERRER || "true").toLowerCase();
  return v !== "false" && v !== "0" && v !== "no";
}

function isHotlinkAllowed(request, env) {
  const origin = request.headers.get("Origin");
  const referer = request.headers.get("Referer");
  const selfHost = new URL(request.url).hostname.toLowerCase();

  if (!origin && !referer) {
    return allowEmptyReferrer(env);
  }

  if (origin && !isAllowedHostFromUrl(origin, selfHost)) {
    return false;
  }
  if (referer && !isAllowedHostFromUrl(referer, selfHost)) {
    return false;
  }
  return true;
}

function isAllowedHostFromUrl(value, selfHost) {
  if (!value || value === "*") return false;
  try {
    const u = new URL(value);
    return isAllowedHostname(u.hostname, selfHost);
  } catch {
    return false;
  }
}

function isAllowedHostname(hostname, selfHost) {
  const host = (hostname || "").toLowerCase();
  if (!host) return false;
  if (selfHost && host === selfHost) return true;
  if (host === "sheikhgo.com" || host.endsWith(".sheikhgo.com")) return true;
  if (host === "localhost" || host === "127.0.0.1") return true;
  if (host.endsWith(".vercel.app")) return true;
  return false;
}

function corsPreflight(request) {
  const headers = new Headers(securityHeaders());
  const origin = request.headers.get("Origin") || "";
  const selfHost = new URL(request.url).hostname.toLowerCase();
  if (origin && isAllowedHostFromUrl(origin, selfHost)) {
    headers.set("Access-Control-Allow-Origin", origin);
    headers.set("Vary", "Origin");
  } else if (origin) {
    // Disallowed origin: do not grant CORS (preflight still 204 without ACAO match)
    headers.set("Vary", "Origin");
  } else {
    headers.set("Access-Control-Allow-Origin", "https://www.sheikhgo.com");
    headers.set("Vary", "Origin");
  }
  headers.set("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS");
  headers.set(
    "Access-Control-Allow-Headers",
    request.headers.get("Access-Control-Request-Headers") || "Range, If-None-Match, If-Modified-Since",
  );
  headers.set("Access-Control-Max-Age", "86400");
  return new Response(null, { status: 204, headers });
}

function withCors(response, request) {
  const headers = new Headers(response.headers);
  const origin = request.headers.get("Origin");
  const selfHost = new URL(request.url).hostname.toLowerCase();
  if (origin && isAllowedHostFromUrl(origin, selfHost)) {
    headers.set("Access-Control-Allow-Origin", origin);
    headers.set("Vary", "Origin");
  } else if (!origin) {
    // Non-browser / direct CDN fetch (empty Origin); ACAO * for simple GETs
    headers.set("Access-Control-Allow-Origin", "*");
  } else {
    headers.set("Vary", "Origin");
  }
  headers.set("Access-Control-Expose-Headers", "Content-Length, Content-Range, Accept-Ranges, ETag");
  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers,
  });
}
