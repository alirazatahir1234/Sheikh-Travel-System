# Bulk user import — Phase 2 & 3 backlog

Phase 1 (shipped in app) covers RBAC role resolution, rich validation UX, dry run, skip duplicates, and error report export.

## Phase 2 — Enterprise workflow (~9.2)

- Mat-stepper dialog: Upload → Validate & map → Resolve → Preview → Import → Results
- Preview summary cards (distinct branches, departments, roles)
- Template menu variants (already partially in dialog; extend with guided copy)
- **Create or update** / **Update only** import modes (backend upsert + role reassignment)
- Branch/department mapping UI for unknown names (dropdown + link to org admin)
- Client-side batching with progress bar (200 rows per request) and cancel
- `libphonenumber` (or equivalent) for phone validation/format
- Role mapping wizard for ambiguous custom role names

## Phase 3 — Platform scale & governance (~9.8)

- Background import job queue + completion notification
- `UserImportBatches` audit table, import history UI, re-import failed rows
- Time-boxed undo last import (soft-delete batch users)
- Optional scheduled imports (SFTP/webhook)
- Optional PDF error report
- Optional AI assist (job title → role suggestion) via existing AI stack

Track implementation in your issue tracker; do not expand Phase 1 scope without completing the above in order.
