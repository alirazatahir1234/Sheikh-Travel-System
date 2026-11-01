const { createReadStream, existsSync, statSync } = require('node:fs');
const path = require('node:path');

const VIDEO_REL = path.join('public', 'website', 'demo', 'sheikhgo-nexus-erp-walkthrough.mp4');
const DEFAULT_API_BASE = 'https://joke-rebates-titans-duties.trycloudflare.com/api';

function resolveVideoPath() {
  const candidates = [
    path.join(process.cwd(), VIDEO_REL),
    path.join(process.cwd(), 'Frontend', 'sheikhgo-erp', VIDEO_REL),
  ];
  for (const candidate of candidates) {
    if (existsSync(candidate)) return candidate;
  }
  return null;
}

function downloadAllowlist() {
  const raw = process.env.DEMO_VIDEO_DOWNLOAD_EMAILS || 'info@sheikhgo.com';
  return raw
    .split(',')
    .map((entry) => entry.trim().toLowerCase())
    .filter(Boolean);
}

async function mayDownload(authHeader) {
  if (!authHeader || !authHeader.startsWith('Bearer ')) return false;

  const apiBase = (process.env.SHEIKHGO_API_URL || DEFAULT_API_BASE).replace(/\/$/, '');
  try {
    const response = await fetch(`${apiBase}/auth/me`, {
      headers: { Authorization: authHeader },
    });
    if (!response.ok) return false;

    const body = await response.json();
    const data = body?.data ?? body?.Data;
    const email = String(data?.email ?? '').trim().toLowerCase();
    if (!email) return false;

    if (downloadAllowlist().includes(email)) return true;

    const role = String(data?.role ?? '').toUpperCase();
    return role === 'SUPER_ADMIN';
  } catch {
    return false;
  }
}

function sendRangeStream(req, res, videoPath, total) {
  const rangeHeader = req.headers.range;
  if (!rangeHeader) return false;

  const match = /^bytes=(\d+)-(\d*)$/i.exec(rangeHeader);
  if (!match) {
    res.status(416).setHeader('Content-Range', `bytes */${total}`).end();
    return true;
  }

  const start = Number.parseInt(match[1], 10);
  let end = match[2] ? Number.parseInt(match[2], 10) : total - 1;
  if (Number.isNaN(start) || Number.isNaN(end) || start >= total || end >= total || start > end) {
    res.status(416).setHeader('Content-Range', `bytes */${total}`).end();
    return true;
  }

  const chunkSize = end - start + 1;
  res.status(206);
  res.setHeader('Content-Range', `bytes ${start}-${end}/${total}`);
  res.setHeader('Content-Length', chunkSize);
  if (req.method === 'HEAD') {
    res.end();
    return true;
  }
  createReadStream(videoPath, { start, end }).pipe(res);
  return true;
}

module.exports = async (req, res) => {
  if (req.method !== 'GET' && req.method !== 'HEAD') {
    res.setHeader('Allow', 'GET, HEAD');
    res.status(405).json({ error: 'Method not allowed' });
    return;
  }

  const videoPath = resolveVideoPath();
  if (!videoPath) {
    res.status(503).json({ error: 'Demo video is not available on this deployment.' });
    return;
  }

  const { size: total } = statSync(videoPath);
  const wantDownload =
    req.query?.download === '1' ||
    req.query?.download === 'true' ||
    req.query?.mode === 'download';

  if (wantDownload) {
    const allowed = await mayDownload(req.headers.authorization);
    if (!allowed) {
      res.status(403).json({ error: 'Authenticated download is restricted to authorized accounts.' });
      return;
    }
    res.setHeader('Content-Type', 'video/mp4');
    res.setHeader(
      'Content-Disposition',
      'attachment; filename="sheikhgo-nexus-erp-walkthrough.mp4"'
    );
    res.setHeader('Content-Length', total);
    res.setHeader('Cache-Control', 'private, no-store');
    if (req.method === 'HEAD') {
      res.status(200).end();
      return;
    }
    createReadStream(videoPath).pipe(res);
    return;
  }

  res.setHeader('Accept-Ranges', 'bytes');
  res.setHeader('Content-Type', 'video/mp4');
  res.setHeader('Content-Disposition', 'inline; filename="playback.mp4"');
  res.setHeader('Cache-Control', 'private, no-store, max-age=0');
  res.setHeader('X-Content-Type-Options', 'nosniff');
  res.setHeader('Cross-Origin-Resource-Policy', 'same-site');

  if (sendRangeStream(req, res, videoPath, total)) return;

  res.setHeader('Content-Length', total);
  if (req.method === 'HEAD') {
    res.status(200).end();
    return;
  }
  createReadStream(videoPath).pipe(res);
};
