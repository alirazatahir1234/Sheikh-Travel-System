/**
 * Client-side heuristics for PK CNIC (and similar ID) OCR line text.
 * Used when Azure returns strong CNIC / lines but weak structured name fields.
 */
export interface CnicOcrParsedFields {
  fullName?: string;
  fatherName?: string;
  cnic?: string;
  address?: string;
  phone?: string;
  email?: string;
  gender?: string;
  dateOfBirth?: string;
  nationality?: string;
}

/** Arabic / Urdu script (NADRA address lines on CNIC back). */
const ARABIC_SCRIPT = /[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF]/;

export function formatAddressUrduThenEnglish(urduBlock: string | undefined, englishBlock: string | undefined): string | undefined {
  const u = urduBlock?.replace(/\s+/g, ' ').trim();
  const e = englishBlock?.replace(/\s+/g, ' ').trim();
  if (u && e) return `${u}\n${e}`;
  return u || e || undefined;
}

/** Form field: prefer English (Latin) address; Urdu only if no English lines were detected. */
export function formatAddressEnglishPreferred(urduBlock: string | undefined, englishBlock: string | undefined): string | undefined {
  const e = englishBlock?.replace(/\s+/g, ' ').trim();
  const u = urduBlock?.replace(/\s+/g, ' ').trim();
  if (e) return e;
  return u || undefined;
}

function countArabicLetters(s: string): number {
  const m = s.match(new RegExp(ARABIC_SCRIPT, 'g'));
  return m?.length ?? 0;
}

function countLatinLetters(s: string): number {
  const m = s.match(/[A-Za-z]/g);
  return m?.length ?? 0;
}

function classifyAddressLineScript(line: string): 'urdu' | 'latin' | 'mixed' {
  const a = countArabicLetters(line);
  const l = countLatinLetters(line);
  if (a >= 4 && l >= 4) return 'mixed';
  if (a >= 3 && l <= a) return 'urdu';
  if (l >= 6 && a < l * 0.5) return 'latin';
  if (a >= 5) return 'urdu';
  if (l >= 8) return 'latin';
  return 'latin';
}

function isAddressNoiseLine(line: string): boolean {
  const t = line.trim();
  if (t.length < 6) return true;
  if (/\b\d{5}-\d{7}-\d\b/.test(t) || /\b\d{13}\b/.test(t.replace(/\D/g, ''))) return true;
  if (/^(?:PAKISTAN|Islamic Republic|Republic|National Identity|Identity Card|Registrar General)/i.test(t)) return true;
  if (/^gumshuda|^گمشدہ/i.test(t)) return true;
  return false;
}

function hasEnglishAddressCue(line: string): boolean {
  return /house|street|road|phase|block|colony|town|sector|city|district|near|st\.|plot|village|tehsil|house\s*no|po\s*box|postal|ضلع/i.test(line);
}

function isMostlyArabicLine(line: string): boolean {
  return countArabicLetters(line) >= 8 && countLatinLetters(line) < countArabicLetters(line) * 0.5;
}

/**
 * If address still contains Urdu + newline + English (legacy), keep Latin-rich lines for the form.
 */
export function finalizeAddressEnglishDisplay(address: string | undefined): string | undefined {
  const raw = address?.replace(/\r/g, '\n').trim();
  if (!raw) return undefined;
  const segments = raw.split('\n').map((s) => s.trim()).filter(Boolean);
  if (segments.length === 1) {
    return segments[0];
  }
  const englishLines = segments.filter((s) => countLatinLetters(s) >= 8 && !isMostlyArabicLine(s));
  if (englishLines.length) return englishLines.join(', ');
  const hintLines = segments.filter((s) => hasEnglishAddressCue(s) && countLatinLetters(s) >= 4);
  if (hintLines.length) return hintLines.join(', ');
  const anyLatin = segments.filter((s) => countLatinLetters(s) >= 12);
  if (anyLatin.length) return anyLatin.join(', ');
  return segments.join(' ').replace(/\s+/g, ' ').trim();
}

/** Azure / Tesseract sometimes return the card banner as the holder name. */
export function sanitizeCnicFullName(fullName: string | undefined | null): string | undefined {
  if (!fullName) return undefined;
  const s = fullName.replace(/\s+/g, ' ').trim();
  if (s.length < 2 || s.length > 120) return undefined;
  const lower = s.toLowerCase();
  if (/\b(islamic\s+republic|national\s+identity|identity\s+card|registrar\s+general)\b/i.test(s)) return undefined;
  if (/\brepublic\s+of\s+pakistan\b/i.test(lower)) return undefined;
  if (/\bnadra\b/i.test(lower)) return undefined;
  if (/\bidentity\s+card\b/i.test(lower) || /\bnational\s+identity\b/i.test(lower)) return undefined;
  if (/\bpakistan\b/.test(lower) && /\b(identity|national\s+identity|republic|islamic)\b/i.test(s)) return undefined;
  if (/\bcnic\b/.test(lower) && lower.includes('page')) return undefined;
  if (/\b(identity|national)\b/i.test(lower) && /\bcard\b/i.test(lower)) return undefined;
  const arabicLetters = (s.match(new RegExp(ARABIC_SCRIPT, 'g')) || []).length;
  const latinLetters = (s.match(/[A-Za-z]/g) || []).length;
  if (arabicLetters >= 4 && latinLetters < 8) {
    return s.length <= 100 ? s : s.slice(0, 100);
  }
  if (!isLikelyLatinPersonNameLine(s)) return undefined;
  return s;
}

/** Latin-only OCR noise (no street cues, vowel-starved tokens) — drop instead of showing in the form. */
function looksLikeOcrGarbageLatinAddress(s: string): boolean {
  if (new RegExp(ARABIC_SCRIPT).test(s)) return false;
  const t = s.replace(/\s+/g, ' ').trim();
  if (t.length < 14) return false;
  if (hasEnglishAddressCue(t)) return false;
  const letters = (t.match(/[A-Za-z]/g) || []).length;
  if (letters < 12) return false;
  const vowels = (t.match(/[aeiouAEIOU]/g) || []).length;
  if (letters > 18 && vowels / letters < 0.2) return true;
  const words = t.split(/\s+/).filter((w) => /^[A-Za-z]{2,}$/.test(w));
  if (words.length >= 5) {
    const avgLen = words.reduce((a, w) => a + w.length, 0) / words.length;
    if (avgLen <= 3.1 && !/\d{2,}/.test(t)) return true;
  }
  return false;
}

export function sanitizeCnicAddress(address: string | undefined | null): string | undefined {
  const base = finalizeAddressEnglishDisplay(address ?? undefined);
  if (!base) return undefined;
  if (looksLikeOcrGarbageLatinAddress(base)) return undefined;
  return base;
}

export function polishCnicParsedFields(f: CnicOcrParsedFields): CnicOcrParsedFields {
  return {
    ...f,
    fullName: sanitizeCnicFullName(f.fullName),
    address: sanitizeCnicAddress(f.address)
  };
}

/** Prefer the holder line that is not card boilerplate (merge was picking the longer banner). */
export function pickBestCnicFullName(a?: string | null, b?: string | null): string | undefined {
  const sa = sanitizeCnicFullName(a?.trim() || undefined);
  const sb = sanitizeCnicFullName(b?.trim() || undefined);
  if (sa && sb) {
    const wa = sa.split(/\s+/).length;
    const wb = sb.split(/\s+/).length;
    if (wa >= 2 && wa <= 6 && (wb > 8 || wb > wa + 4)) return sa;
    if (wb >= 2 && wb <= 6 && (wa > 8 || wa > wb + 4)) return sb;
    return wa <= wb ? sa : sb;
  }
  return sa || sb;
}

function splitMixedUrduLatinLine(line: string): { urdu?: string; english?: string } {
  const idx = line.search(/[A-Za-z]{4,}/);
  if (idx >= 8 && countArabicLetters(line.slice(0, idx)) >= 4) {
    return {
      urdu: line.slice(0, idx).trim(),
      english: line.slice(idx).trim()
    };
  }
  return {};
}

/**
 * NADRA CNIC: collect Urdu and English address lines; form shows English when available.
 */
export function extractAddressUrduThenEnglish(lines: string[], side: 'front' | 'back'): string | undefined {
  const urduParts: string[] = [];
  const englishParts: string[] = [];
  const seen = new Set<string>();

  const pushUnique = (arr: string[], part: string) => {
    const k = part.replace(/\s+/g, ' ').trim();
    if (k.length < 6 || seen.has(k)) return;
    seen.add(k);
    arr.push(k);
  };

  for (const raw of lines) {
    const line = raw.trim();
    if (!line || isAddressNoiseLine(line)) continue;

    if (/^(?:permanent\s+)?(?:address|addr\.?|پتہ)\s*[:\s.-]*/i.test(line)) {
      const rest = line.replace(/^(?:permanent\s+)?(?:address|addr\.?|پتہ)\s*[:\s.-]*/i, '').trim();
      if (rest.length < 6) continue;
      const script = classifyAddressLineScript(rest);
      if (script === 'mixed') {
        const sp = splitMixedUrduLatinLine(rest);
        if (sp.urdu) pushUnique(urduParts, sp.urdu);
        if (sp.english) pushUnique(englishParts, sp.english);
      } else if (script === 'urdu') {
        pushUnique(urduParts, rest);
      } else if (hasEnglishAddressCue(rest) || rest.length >= 12) {
        pushUnique(englishParts, rest);
      }
      continue;
    }

    const script = classifyAddressLineScript(line);
    if (script === 'mixed') {
      const sp = splitMixedUrduLatinLine(line);
      if (sp.urdu) pushUnique(urduParts, sp.urdu);
      if (sp.english) pushUnique(englishParts, sp.english);
      continue;
    }
    if (script === 'urdu' && line.length >= 10) {
      if (/^موجودہ\s*پتہ/i.test(line)) continue;
      pushUnique(urduParts, line);
      continue;
    }
    if (script === 'latin' && line.length >= 14 && (hasEnglishAddressCue(line) || (side === 'back' && line.length >= 22))) {
      pushUnique(englishParts, line);
    }
  }

  if (side === 'back' && englishParts.length === 0) {
    for (const raw of lines) {
      const line = raw.trim();
      if (!line || isAddressNoiseLine(line)) continue;
      if (classifyAddressLineScript(line) !== 'latin') continue;
      if (line.length < 20 || countLatinLetters(line) < 14) continue;
      if (!/\d/.test(line) && line.length < 36) continue;
      if (/\b(Father|Mother|Name|Gender|Country|Date|Identity)\b/i.test(line)) continue;
      pushUnique(englishParts, line);
    }
  }

  const urduBlock = urduParts.join(' ').trim();
  const englishBlock = englishParts.join(', ').trim();
  return formatAddressEnglishPreferred(urduBlock || undefined, englishBlock || undefined);
}

/**
 * Merge front/back OCR address strings; prefer English for display.
 */
export function mergeCnicSideAddresses(front?: string, back?: string): string | undefined {
  const f = front?.replace(/\r/g, '').trim();
  const b = back?.replace(/\r/g, '').trim();
  if (!f) return finalizeAddressEnglishDisplay(b || undefined);
  if (!b) return finalizeAddressEnglishDisplay(f || undefined);

  const arabicF = countArabicLetters(f) >= 6;
  const arabicB = countArabicLetters(b) >= 6;
  const latinF = countLatinLetters(f) >= 12;
  const latinB = countLatinLetters(b) >= 12;

  let merged: string | undefined;
  if (f.includes('\n') && !b.includes('\n')) merged = f;
  else if (b.includes('\n') && !f.includes('\n')) merged = b;
  else if (f.includes('\n') && b.includes('\n')) merged = b.length >= f.length ? b : f;
  else if (arabicF && latinB && !arabicB) merged = formatAddressEnglishPreferred(f, b);
  else if (arabicB && latinF && !latinB) merged = formatAddressEnglishPreferred(b, f);
  else if (arabicF && arabicB) merged = b.length >= f.length ? b : f;
  else if (latinF && latinB) merged = b.length >= f.length ? b : f;
  else merged = b.length >= f.length ? b : f;

  return finalizeAddressEnglishDisplay(merged);
}

/**
 * NADRA CNIC back: permanent address (Mustaqil pata / مستقل پتہ) as Urdu text.
 * Server translates to English; client OCR uses this to avoid picking Maujuda (current) first.
 */
export function extractPermanentUrduAddressFromRaw(rawText: string): string | undefined {
  const t = rawText.replace(/\r/g, '\n');
  const withBreaks = t
    .replace(/(?<=[^\n])(?=\s*موجودہ\s*پتہ)/g, '\n')
    .replace(/(?<=[^\n])(?=\s*مستقل\s*پتہ)/g, '\n');
  const lines = withBreaks.split('\n').map((l) => l.trim()).filter(Boolean);
  let start = -1;
  for (let i = 0; i < lines.length; i++) {
    if (/مستقل\s*پتہ|PERMANENT\s+ADDRESS/i.test(lines[i])) {
      start = i;
      break;
    }
  }
  if (start < 0) {
    const m = t.match(
      /مستقل\s*پتہ\s*[:\s\u0640.-]*\s*([^\n]+?)(?=\s*موجودہ\s*پتہ|CURRENT\s+ADDRESS|$)/i
    );
    if (!m?.[1]) return undefined;
    const u = m[1].trim();
    return u.length >= 6 && countArabicLetters(u) >= 4 ? u : undefined;
  }
  const parts: string[] = [];
  const first = lines[start].replace(/^.*?(?:مستقل\s*پتہ|PERMANENT\s+ADDRESS)\s*[:\s.-]*/i, '').trim();
  if (first.length >= 6 && countArabicLetters(first) >= 4) parts.push(first);
  for (let j = start + 1; j < lines.length; j++) {
    const line = lines[j];
    if (line.length < 6) break;
    if (/موجودہ\s*پتہ|CURRENT\s+ADDRESS|TEMPORARY/i.test(line)) break;
    if (isAddressNoiseLine(line)) break;
    parts.push(line);
    if (parts.length >= 6) break;
  }
  return parts.length ? parts.join(' ').replace(/\s+/g, ' ').trim() : undefined;
}

/** OCR typo-tolerant "Father" line (holder name is usually on the line above). */
const FATHER_LINE_RE = /\b(?:Father|Fathers|Father's|FATHER|Fathar|Fther|Faher|F ather|F\/Father)\b/i;

/**
 * Re-parse front+back raw OCR together so the holder name can be found when it only appears
 * relative to lines from the other side (e.g. Father on front, extra tokens split across images).
 */
export function enrichCnicParsedFromCombinedRaw(
  merged: CnicOcrParsedFields,
  rawFront: string | null | undefined,
  rawBack: string | null | undefined
): CnicOcrParsedFields {
  const rf = (rawFront || '').trim();
  const rb = (rawBack || '').trim();
  if (!rf && !rb) return merged;
  const combined = [rf, rb].filter(Boolean).join('\n');
  if (combined.length < 24) return merged;
  const fromCombined = parseCnicDocumentOcrText(combined, 'front');
  return polishCnicParsedFields({
    fullName: pickBestCnicFullName(merged.fullName, fromCombined.fullName),
    fatherName: merged.fatherName || fromCombined.fatherName,
    cnic: merged.cnic || fromCombined.cnic,
    address: mergeCnicSideAddresses(merged.address, fromCombined.address),
    phone: merged.phone || fromCombined.phone,
    email: merged.email || fromCombined.email,
    gender: merged.gender || fromCombined.gender,
    dateOfBirth: merged.dateOfBirth || fromCombined.dateOfBirth,
    nationality: merged.nationality || fromCombined.nationality
  });
}

export function parseCnicDocumentOcrText(text: string, side: 'front' | 'back' = 'front'): CnicOcrParsedFields {
  const normalized = text
    .replace(/\r/g, '\n')
    .replace(/(?<=[^\n])(?=\s*موجودہ\s*پتہ)/g, '\n')
    .replace(/(?<=[^\n])(?=\s*مستقل\s*پتہ)/g, '\n');
  const lines = normalized.split('\n').map((l) => l.trim()).filter(Boolean);

  const cnicMatch =
    normalized.match(/\b\d{5}-\d{7}-\d\b/) ||
    normalized.match(/\b\d{13}\b/);
  const cnic = cnicMatch?.[0]
    ? cnicMatch[0].includes('-')
      ? cnicMatch[0]
      : `${cnicMatch[0].slice(0, 5)}-${cnicMatch[0].slice(5, 12)}-${cnicMatch[0].slice(12)}`
    : undefined;

  let fullName: string | undefined =
    extractHolderNameBeforeFatherFromBlob(normalized) ||
    extractHolderNameLineBeforeFather(lines) ||
    extractHolderNameNearCnicLine(lines);

  if (!fullName) {
    const m = normalized.match(/(?:^|\n)\s*Name\s*[:\s.-]+\s*([A-Za-z][^\n]+)/i);
    if (m?.[1] && !FATHER_LINE_RE.test(m[1])) {
      const candidate = m[1].replace(/\s+/g, ' ').trim();
      if (candidate.length >= 4 && candidate.length <= 120 && /^[A-Za-z][A-Za-z\s.'-]+$/.test(candidate)) {
        fullName = candidate;
      }
    }
  }

  const ignoreTokens = [
    'islamic republic',
    'national identity',
    'pakistan',
    'card',
    'father',
    'husband',
    'date of birth',
    'gender',
    'cnic page'
  ];
  if (!fullName) {
    for (const line of lines) {
      const lower = line.toLowerCase();
      if (line.length < 3 || line.length > 120) continue;
      if (/\d{5}-\d{7}-\d/.test(line)) continue;
      if (ignoreTokens.some((t) => lower.includes(t))) continue;
      if (/\bcnic\b/.test(lower) && lower.includes('page')) continue;
      if (FATHER_LINE_RE.test(line)) continue;
      if (/^[A-Z][A-Z\s.]+$/.test(line) || /^[A-Z][a-z]+(?:\s+[A-Z][a-z]+)+$/.test(line)) {
        fullName = line.replace(/\s+/g, ' ').trim();
        break;
      }
    }
  }

  let address = extractAddressUrduThenEnglish(lines, side);
  const permUrdu = extractPermanentUrduAddressFromRaw(normalized);
  if (permUrdu) {
    const generic = address;
    const genericLatinRich =
      !!generic && countLatinLetters(generic) >= 10 && countArabicLetters(generic) < 6;
    address = genericLatinRich ? generic : permUrdu;
  }

  if (!address) {
    const addressLine = lines.find((l) => /address|addr\.|پتہ|permanent|residence/i.test(l));
    if (addressLine) {
      const rest = addressLine
        .replace(/^(?:permanent\s+)?(?:address|addr\.?|پتہ)\s*[:\s.-]*/i, '')
        .trim();
      if (rest.length >= 6) address = rest;
    }
  }

  if (!address && side === 'back') {
    const addrHints = /house|street|road|phase|block|colony|town|sector|city|district|near|st\.|plot|village|tehsil/i;
    const bodyLines = lines.filter(
      (l) =>
        l.length > 18 &&
        !/\b\d{5}-\d{7}-\d\b/.test(l) &&
        !/^\d{13}$/.test(l.replace(/\D/g, '')) &&
        addrHints.test(l)
    );
    if (bodyLines.length) {
      address = formatAddressEnglishPreferred(undefined, bodyLines.slice(0, 4).join(', ').replace(/\s+/g, ' ').trim());
    }
  }

  address = finalizeAddressEnglishDisplay(address);

  const fatherName = extractFatherNameFromNormalized(normalized, lines);

  const genderMatch = normalized.match(/\bGender\s*[:\s.-]*\s*([MF])\b/i);
  const gender = genderMatch
    ? genderMatch[1].toUpperCase() === 'M'
      ? 'Male'
      : 'Female'
    : undefined;

  let dateOfBirth: string | undefined;
  const dobMatch = normalized.match(/\bDate\s+of\s+Birth\s*[:\s.-]*\s*(\d{2})\.(\d{2})\.(\d{4})\b/i);
  if (dobMatch) {
    const d = +dobMatch[1];
    const mo = +dobMatch[2];
    const y = +dobMatch[3];
    const dt = new Date(Date.UTC(y, mo - 1, d));
    if (!Number.isNaN(dt.valueOf())) dateOfBirth = dt.toISOString().slice(0, 10);
  }

  const nationality =
    /Country\s+of\s+Stay\s*[:\s.-]*\s*Pakistan/i.test(normalized) ||
    /Nationality\s*[:\s.-]*\s*Pakistan/i.test(normalized)
      ? 'Pakistani'
      : undefined;

  const { phone, email } = extractPhoneAndEmailFromText(normalized);

  return polishCnicParsedFields({
    fullName,
    fatherName,
    cnic,
    address,
    phone,
    email,
    gender,
    dateOfBirth,
    nationality
  });
}

function extractFatherNameFromNormalized(normalized: string, lines: string[]): string | undefined {
  const inline = normalized.match(
    /(?:Father|Husband)(?:'s|\s*)?\s*Name\s*[:\s.-]+\s*([^\n\r]{3,120})/i
  );
  if (inline?.[1]) {
    const n = inline[1].replace(/\s+/g, ' ').trim().replace(/\s*[-–—|]+\s*$/, '');
    if (isLikelyLatinPersonNameLine(n)) return n;
  }
  const fatherIdx = lines.findIndex((l) => FATHER_LINE_RE.test(l));
  if (fatherIdx >= 0) {
    const same = lines[fatherIdx];
    const m2 = same.match(/^(?:Father|Husband)(?:'s|\s*)?\s*Name\s*[:\s.-]+\s*(.+)$/i);
    if (m2?.[1]) {
      const n2 = m2[1].replace(/\s+/g, ' ').trim();
      if (isLikelyLatinPersonNameLine(n2)) return n2;
    }
    if (fatherIdx + 1 < lines.length) {
      const next = lines[fatherIdx + 1].replace(/\s+/g, ' ').trim();
      if (isLikelyLatinPersonNameLine(next) && !/^(?:Mother|Gender|Country)/i.test(next)) return next;
    }
  }
  const urduFather = normalized.match(
    /(?:والد|والدہ)\s*(?:کا\s*)?نام\s*[:\s\u0640.-]+\s*([^\n\r]{3,120})/u
  );
  if (urduFather?.[1]) {
    const n3 = urduFather[1].replace(/\s+/g, ' ').trim().replace(/\s*[-–—|]+\s*$/, '');
    if (n3.length >= 3 && (isLikelyLatinPersonNameLine(n3) || countArabicLetters(n3) >= 4)) return n3;
  }
  return undefined;
}

/** Holder name often appears a few lines above the CNIC number on the front. */
function extractHolderNameNearCnicLine(lines: string[]): string | undefined {
  const cnicRe = /\b\d{5}-\d{7}-\d\b/;
  let idx = lines.findIndex((l) => cnicRe.test(l));
  if (idx < 0) {
    const compact = lines.findIndex((l) => /\d{13}/.test(l.replace(/\D/g, '')));
    idx = compact;
  }
  if (idx <= 0) return undefined;
  for (let j = idx - 1; j >= 0 && j >= idx - 6; j--) {
    const line = lines[j].replace(/\s+/g, ' ').trim();
    if (line.length < 5 || line.length > 120) continue;
    if (cnicRe.test(line)) continue;
    if (/^(?:Date|Gender|Country|Islamic|PAKISTAN|Identity|National|Card|Republic|Address|Expiry)/i.test(line)) continue;
    if (FATHER_LINE_RE.test(line)) continue;
    const tokens = line.split(/\s+/).filter(Boolean);
    if (tokens.length < 2) continue;
    if (!/^[A-Za-z]/.test(line)) continue;
    if (/^[\x00-\x7F]+$/.test(line) && isLikelyLatinPersonNameLine(line)) return line;
  }
  return undefined;
}

function isLikelyLatinPersonNameLine(line: string): boolean {
  const letters = line.replace(/[^A-Za-z]/g, '').length;
  if (letters < 6 || letters / Math.max(line.length, 1) < 0.55) return false;
  return /^[A-Za-z][A-Za-z\s.'-]+$/.test(line);
}

/** Azure may join lines; find text before first "Father" and take last plausible Latin holder line. */
function extractHolderNameBeforeFatherFromBlob(text: string): string | undefined {
  const idx = text.search(FATHER_LINE_RE);
  if (idx <= 0) return undefined;
  const before = text.slice(0, idx).trim();
  const lines = before.split(/[\n\r]+/).map((l) => l.trim()).filter(Boolean);
  for (let i = lines.length - 1; i >= 0; i--) {
    const line = lines[i].replace(/\s+/g, ' ').trim();
    if (line.length < 4 || line.length > 120) continue;
    if (/\b\d{5}-\d{7}-\d\b/.test(line)) continue;
    if (/^(?:Name|Mother|Country|Gender|Date|Identity|National|Card)\b/i.test(line)) continue;
    const tokens = line.split(/\s+/).filter(Boolean);
    if (tokens.length >= 2 && isLikelyLatinPersonNameLine(line)) return line;
  }

  const m = text.match(
    /(?:^|[\n\r])([^\n\r]{5,120})[\n\r]+[^\n\r]*(?:Father|Fathers|Father's|FATHER|Fathar|Fther|Faher)\b/i
  );
  if (!m?.[1]) return undefined;
  const line2 = m[1].trim().replace(/\s+/g, ' ');
  if (line2.length < 4 || /\b\d{5}-\d{7}-\d\b/.test(line2)) return undefined;
  if (/^(?:Name|Mother|Country|Gender|Date|Identity|National|Card)\b/i.test(line2)) return undefined;
  const tokens2 = line2.split(/\s+/).filter(Boolean);
  if (tokens2.length >= 2 && isLikelyLatinPersonNameLine(line2)) return line2;
  return undefined;
}

function extractHolderNameLineBeforeFather(lines: string[]): string | undefined {
  const fatherIdx = lines.findIndex((l) => FATHER_LINE_RE.test(l));
  if (fatherIdx <= 0) return undefined;
  for (let j = fatherIdx - 1; j >= 0; j--) {
    const line = lines[j].replace(/\s+/g, ' ').trim();
    if (line.length < 4 || line.length > 120) continue;
    if (/\b\d{5}-\d{7}-\d\b/.test(line)) continue;
    if (/^(?:Name|Mother|Country|Gender|Date|Identity|National|Card)\b/i.test(line)) continue;
    const tokens = line.split(/\s+/).filter(Boolean);
    if (tokens.length >= 2 && isLikelyLatinPersonNameLine(line)) return line;
  }
  return undefined;
}

function extractPhoneAndEmailFromText(text: string): { phone?: string; email?: string } {
  const emailMatch = text.match(/\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b/i);
  const email = emailMatch ? emailMatch[0].trim().toLowerCase() : undefined;

  const pkMobile =
    text.match(/\b(?:\+92[\s-]?|0092[\s-]?|92[\s-]?|0)3\d{2}[\s-]?\d{7}\b/) ||
    text.match(/\b\+92\s?3\d{2}\s?\d{7}\b/);
  const land = !pkMobile ? text.match(/\b0\d{2,3}[\s-]?\d{6,9}\b/) : null;
  const rawPhone = pkMobile?.[0] ?? land?.[0];
  const phone = rawPhone ? normalizePkPhoneForForm(rawPhone) : undefined;

  return { phone, email };
}

/** Collapse PK-style numbers to a compact display (max 20 chars for typical forms). */
function normalizePkPhoneForForm(raw: string): string {
  let s = raw.replace(/[^\d+]/g, '');
  if (s.startsWith('0092')) s = '+92' + s.slice(4);
  else if (s.startsWith('92') && !s.startsWith('+') && s.length >= 10 && s[2] === '3') s = '+' + s;
  if (s.length > 20) s = s.slice(0, 20);
  return s;
}

/**
 * Loads an image, auto-rotates portrait scans to landscape (common wrong phone orientation for CNIC),
 * scales to max dimension, and returns a JPEG data URL plus a File for API upload (same pixels as preview).
 */
export function compressCnicImageToJpeg(
  file: File,
  maxDim = 1600,
  quality = 0.9
): Promise<{ dataUrl: string; jpegFile: File }> {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      URL.revokeObjectURL(url);
      try {
        const nw = img.naturalWidth || img.width;
        const nh = img.naturalHeight || img.height;
        const rotated = document.createElement('canvas');
        const rctx = rotated.getContext('2d');
        if (!rctx) {
          reject(new Error('Canvas unsupported'));
          return;
        }
        let rw = nw;
        let rh = nh;
        if (nh > nw * 1.08) {
          rotated.width = nh;
          rotated.height = nw;
          rctx.translate(rotated.width, 0);
          rctx.rotate(Math.PI / 2);
          rctx.drawImage(img, 0, 0, nw, nh);
          rw = nh;
          rh = nw;
        } else {
          rotated.width = nw;
          rotated.height = nh;
          rctx.drawImage(img, 0, 0, nw, nh);
        }

        const scale = rw > maxDim || rh > maxDim ? maxDim / Math.max(rw, rh) : 1;
        const fw = Math.max(1, Math.round(rw * scale));
        const fh = Math.max(1, Math.round(rh * scale));
        const out = document.createElement('canvas');
        out.width = fw;
        out.height = fh;
        const octx = out.getContext('2d');
        if (!octx) {
          reject(new Error('Canvas unsupported'));
          return;
        }
        octx.drawImage(rotated, 0, 0, rw, rh, 0, 0, fw, fh);
        const dataUrl = out.toDataURL('image/jpeg', quality);
        out.toBlob(
          (blob) => {
            if (!blob) {
              reject(new Error('toBlob failed'));
              return;
            }
            const base = (file.name || 'cnic').replace(/\.[^.]+$/i, '');
            resolve({
              dataUrl,
              jpegFile: new File([blob], `${base || 'cnic'}.jpg`, { type: 'image/jpeg' })
            });
          },
          'image/jpeg',
          quality
        );
      } catch (e) {
        reject(e);
      }
    };
    img.onerror = () => {
      URL.revokeObjectURL(url);
      reject(new Error('Image load failed'));
    };
    img.src = url;
  });
}
