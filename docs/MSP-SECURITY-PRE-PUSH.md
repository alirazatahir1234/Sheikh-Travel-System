# Standing rule — MSP / SheikhGo Security MCP before every push

**Owner:** Ali  
**Applies to:** All agents pushing from `Sheikh-Travel-System`  
**Source:** [SheikhGo-MCP](../../SheikhGo-MCP) Security scanners (`scan_secrets`, `audit_api_auth`, `audit_permissions`) + architecture `review_change`

Nothing insecure gets pushed. **Scan → fix → re-scan → only then `git push`.**

## Preferred: Cursor MCP namespace

1. `GetDynamicTools` pattern `(?i)sheikh|security|scan_secrets|review_change`
2. Expected: `user-sheikhgo` (config key `sheikhgo` → `/Users/alirazatahir/Projects/SheikhGo-MCP`)
3. If missing: ensure project `.cursor/mcp.json` wires `sheikhgo`, then Reload MCP

## Offline fallback (same scanners)

```bash
cd /Users/alirazatahir/Projects/SheikhGo-MCP
export SHEIKHGO_REPOSITORY=/Users/alirazatahir/Projects/Sheikh-Travel-System
# Write staged/working diff to /tmp/msp-diff.patch first
node --input-type=module <<'JS'
import { readFileSync } from 'node:fs';
import { loadConfig } from './dist/config/config.js';
import { scanSecrets } from './dist/security/secret-scanner.js';
import { reviewChangeDetailed } from './dist/architecture/change-reviewer.js';
const config = loadConfig();
const diff = readFileSync('/tmp/msp-diff.patch', 'utf8');
const review = reviewChangeDetailed(config, { diffText: diff, maxFindings: 50 });
const secrets = scanSecrets(config, { pathPrefix: 'Backend/', limit: 100 });
const high = (review.findings || []).filter(f => /high|critical/i.test(String(f.severity || '')));
if (high.length || (secrets.findings || []).length) {
  console.error(JSON.stringify({ high, secrets: secrets.findings }, null, 2));
  process.exit(2);
}
console.log('MSP gate clean');
JS
```

## Gate checklist

- [ ] `review_change` on the push diff — high/critical = 0
- [ ] `scan_secrets` on changed paths — findings = 0 (or fixed + re-scanned)
- [ ] Do not push secrets, credentials, or unexpected public API endpoints introduced by the change
- [ ] Push GitHub `origin` only; ignore Azure pushurl auth noise
