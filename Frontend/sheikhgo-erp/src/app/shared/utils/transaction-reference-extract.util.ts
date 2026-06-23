/**
 * Best-effort extraction of a bank / wallet transaction reference from a receipt
 * file name (e.g. TRANSACTION_RECEIPT_50F58245F589.pdf) or from OCR text.
 */

const NAME_PATTERNS: RegExp[] = [
  /TRANSACTION[_\s-]*RECEIPT[_\s-]*([A-Z0-9]{6,})/i,
  /RECEIPT[_\s-]*([A-Z0-9]{6,})/i,
  /(?:TXN|TXID|TXNID|REF|REFERENCE|UTR|RRN)[_\s:-]*([A-Z0-9-]{6,40})/i,
  /PAYMENT[_\s-]*(?:ID|NO)[_\s:-]*([A-Z0-9-]{6,40})/i
];

export function extractTransactionReferenceFromFileName(fileName: string): string | null {
  const base = fileName.replace(/\.[^/.]+$/, '').trim();
  for (const re of NAME_PATTERNS) {
    const m = base.match(re);
    if (m?.[1]) return normalizeRefToken(m[1]);
  }
  const tokens = base
    .split(/[^A-Za-z0-9]+/)
    .filter(t => /^[A-Za-z0-9]{8,40}$/.test(t));
  if (tokens.length === 0) return null;
  tokens.sort((a, b) => b.length - a.length);
  return normalizeRefToken(tokens[0]);
}

function normalizeRefToken(s: string): string {
  return s.replace(/^[-_:]+|[-_:]+$/g, '').toUpperCase();
}

const OCR_LINE_PATTERNS: RegExp[] = [
  /(?:transaction|txn|transfer)\s*(?:id|no\.?|#|number)\s*[:\s]+([A-Z0-9-]{6,40})/i,
  /(?:reference|ref\.?|rrn|utr|trace|auth)\s*(?:no\.?|#|number|id)?\s*[:\s]+([A-Z0-9-]{6,40})/i,
  /(?:receipt|confirmation)\s*(?:#|no\.?)?\s*[:\s]*([A-Z0-9-]{6,40})/i,
  /(?:order|payment)\s*id\s*[:\s]+([A-Z0-9-]{6,40})/i
];

export function extractTransactionReferenceFromReceiptOcrText(raw: string): string | null {
  const text = raw.replace(/\s+/g, ' ').trim();
  if (!text) return null;

  for (const re of OCR_LINE_PATTERNS) {
    const m = text.match(re);
    if (m?.[1]) {
      const v = normalizeRefToken(m[1]);
      if (v.length >= 6 && v.length <= 40) return v;
    }
  }

  const digitRuns = text.match(/\b\d{8,20}\b/g);
  if (digitRuns?.length) {
    const best = digitRuns.sort((a, b) => b.length - a.length)[0];
    return best;
  }

  const mixed = text.match(/\b[A-Z0-9]{10,32}\b/gi);
  if (mixed?.length) {
    const best = mixed.sort((a, b) => b.length - a.length)[0];
    return normalizeRefToken(best);
  }

  return null;
}
