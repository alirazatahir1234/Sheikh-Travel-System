import { Component, ElementRef, Inject, ViewChild } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import * as XLSX from 'xlsx';
import { UserService } from '../../../core/services/user.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { CreateUserDto, UserRoleLabels } from '../../../core/models/user.model';
import { RoleSummary } from '../../../core/models/platform.model';
import {
  USER_IMPORT_SAMPLE,
  USER_IMPORT_SAMPLE_ROWS,
  USER_IMPORT_HEADERS,
  getUserImportExportColumns,
  parseUserImportRecords,
  reparseImportRow,
  buildImportErrorReport,
  ParsedUserImportRow,
  UserImportParseContext,
  UserImportRow,
  ImportErrorReportRow
} from '../user-import-format';
import { PRIMARY_ROLE_CATALOG, permissionCountForPrimary } from '../user-primary-roles';

export interface BulkUserImportDialogData {
  branches: { id: number; name: string }[];
  departments: { id: number; name: string }[];
  assignableRoles: RoleSummary[];
  isPlatformSuperAdmin: boolean;
  existingEmails: string[];
  existingPhones: string[];
  existingEmployeeCodes: string[];
}

export interface BulkUserImportDialogResult {
  created: number;
  failed: number;
}

interface PreviewRow extends ParsedUserImportRow {
  temporaryPassword?: string | null;
  serverError?: string | null;
  serverSkipped?: string | null;
  dryRunOk?: boolean;
  created?: boolean;
  /** Override role pick in UI */
  roleOverrideCode?: string;
}

@Component({
  standalone: false,
  selector: 'app-bulk-user-import-dialog',
  templateUrl: './bulk-user-import-dialog.component.html',
  styleUrls: ['./bulk-user-import-dialog.component.scss']
})
export class BulkUserImportDialogComponent {
  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;

  fileName = '';
  rows: PreviewRow[] = [];
  submitting = false;
  done = false;

  skipDuplicates = true;
  ignoreInvalidRows = false;
  generatePasswords = true;
  dryRun = false;

  private parseContext!: UserImportParseContext;

  constructor(
    private dialogRef: MatDialogRef<BulkUserImportDialogComponent, BulkUserImportDialogResult>,
    @Inject(MAT_DIALOG_DATA) public data: BulkUserImportDialogData,
    private userService: UserService,
    private exportService: ExportService,
    private toast: UiToastService
  ) {
    this.parseContext = {
      branches: data.branches,
      departments: data.departments,
      assignableRoles: data.assignableRoles,
      isPlatformSuperAdmin: data.isPlatformSuperAdmin,
      existingEmails: new Set(data.existingEmails.map(e => e.toLowerCase())),
      existingPhones: new Set(data.existingPhones.map(p => p.replace(/\D/g, ''))),
      existingEmployeeCodes: new Set(data.existingEmployeeCodes.map(c => c.toLowerCase()))
    };
  }

  get totalCount(): number {
    return this.rows.length;
  }

  get readyCount(): number {
    return this.rows.filter(r => r.severity === 'ready' && !!r.dto).length;
  }

  get warningCount(): number {
    return this.rows.filter(r => r.severity === 'warning' && !!r.dto).length;
  }

  get errorCount(): number {
    return this.rows.filter(r => r.severity === 'error' || !r.dto).length;
  }

  get importableCount(): number {
    return this.rows.filter(r => this.isRowImportable(r)).length;
  }

  get createdCount(): number {
    return this.rows.filter(r => r.created).length;
  }

  get hasTemporaryPasswords(): boolean {
    return this.rows.some(r => !!r.temporaryPassword);
  }

  get temporaryPasswordRows(): PreviewRow[] {
    return this.rows.filter(r => !!r.temporaryPassword);
  }

  get autoFixMessages(): string[] {
    const msgs = new Set<string>();
    for (const row of this.rows) {
      if (row.resolvedRole?.confidence === 'fuzzy') {
        msgs.add(`Role "${row.resolvedRole.mappedFrom}" → ${row.resolvedRole.platformRoleCode}`);
      }
      if (row.displayPhone && row.dto?.phone && row.displayPhone !== row.dto.phone) {
        msgs.add(`Phone formatted for ${row.dto.email}`);
      }
      for (const issue of row.issues) {
        if (issue.code.startsWith('dup_')) {
          msgs.add(issue.message);
        }
      }
    }
    return [...msgs].slice(0, 8);
  }

  get previewSummary(): { users: number; branches: number; departments: number; roles: string[] } {
    const branches = new Set<string>();
    const departments = new Set<string>();
    const roles = new Set<string>();
    for (const row of this.rows) {
      if (row.branchStatus && row.branchStatus !== '—') branches.add(row.branchStatus);
      if (row.departmentStatus && row.departmentStatus !== '—') departments.add(row.departmentStatus);
      if (row.resolvedRole) roles.add(row.resolvedRole.displayLabel);
    }
    return {
      users: this.rows.length,
      branches: branches.size,
      departments: departments.size,
      roles: [...roles]
    };
  }

  get importDisabledReason(): string | null {
    if (this.submitting || this.done) return null;
    if (this.importableCount === 0) return 'No rows are ready to import.';
    if (this.errorCount > 0 && !this.ignoreInvalidRows) {
      return `Cannot import because ${this.errorCount} row(s) contain errors. Fix them, enable "Ignore invalid rows", or remove those rows.`;
    }
    return null;
  }

  roleOptions(): { code: string; label: string }[] {
    const seen = new Set<string>();
    const out: { code: string; label: string }[] = [];
    for (const def of PRIMARY_ROLE_CATALOG) {
      if (seen.has(def.platformCode)) continue;
      seen.add(def.platformCode);
      out.push({ code: def.platformCode, label: def.label });
    }
    for (const r of this.data.assignableRoles) {
      if (!r.isActive) continue;
      const code = r.code.toUpperCase();
      if (seen.has(code)) continue;
      seen.add(code);
      out.push({ code, label: r.displayName || r.name });
    }
    return out.sort((a, b) => a.label.localeCompare(b.label));
  }

  roleLabel(role: number): string {
    return UserRoleLabels[role as keyof typeof UserRoleLabels] ?? String(role);
  }

  permissionHint(row: PreviewRow): string {
    if (!row.resolvedRole) return '';
    const n = permissionCountForPrimary(
      row.resolvedRole.primaryDef ?? {
        id: row.resolvedRole.platformRoleCode,
        label: row.resolvedRole.displayLabel,
        platformCode: row.resolvedRole.platformRoleCode,
        legacyRole: row.resolvedRole.legacyRole,
        description: '',
        modules: [],
        approxPermissions: 0,
        financialAccess: 'none',
        defaultWorkspaceKey: '',
        defaultHomeRoute: '',
        defaultDashboardKey: '',
        workspaceLabel: '',
        dashboardLabel: ''
      },
      this.data.assignableRoles
    );
    const mods = row.resolvedRole.primaryDef?.modules?.slice(0, 4).join(', ') ?? '';
    return `${row.resolvedRole.platformRoleCode} · ~${n} permissions${mods ? ' · ' + mods : ''}`;
  }

  primaryIssue(row: PreviewRow): string {
    if (row.serverError) return row.serverError;
    if (row.serverSkipped) return row.serverSkipped;
    const err = row.issues.find(i => i.severity === 'error');
    if (err) return err.message;
    const warn = row.issues.find(i => i.severity === 'warning');
    return warn?.message ?? '';
  }

  primarySuggestion(row: PreviewRow): string {
    if (row.serverSkipped) return 'Skipped';
    const err = row.issues.find(i => i.severity === 'error');
    if (err?.suggestion) return err.suggestion;
    const warn = row.issues.find(i => i.severity === 'warning');
    return warn?.suggestion ?? '';
  }

  isRowImportable(row: PreviewRow): boolean {
    if (!row.dto) return false;
    if (row.severity === 'error' && !this.ignoreInvalidRows) return false;
    return true;
  }

  downloadTemplateCsv(basic = true): void {
    const rows = basic ? [USER_IMPORT_SAMPLE] : USER_IMPORT_SAMPLE_ROWS;
    this.exportService.exportCsv(rows, getUserImportExportColumns(), {
      filename: basic ? 'users-import-basic-template' : 'users-import-sample-data',
      sheetName: 'Users'
    });
  }

  downloadTemplateExcel(basic = true): void {
    const rows = basic ? [USER_IMPORT_SAMPLE] : USER_IMPORT_SAMPLE_ROWS;
    this.exportService.exportExcel(rows, getUserImportExportColumns(), {
      filename: basic ? 'users-import-basic-template' : 'users-import-sample-data',
      sheetName: 'Users',
      title: basic ? 'Users import (basic)' : 'Users import (sample data)'
    });
  }

  downloadEnterpriseTemplate(): void {
    const empty = { ...USER_IMPORT_SAMPLE };
    for (const h of USER_IMPORT_HEADERS) {
      (empty as unknown as Record<string, string>)[h] = '';
    }
    this.exportService.exportExcel([empty], getUserImportExportColumns(), {
      filename: 'users-import-enterprise-template',
      sheetName: 'Users',
      title: 'Users import (enterprise columns)'
    });
  }

  downloadErrorReportCsv(): void {
    this.exportErrorReport('csv');
  }

  downloadErrorReportExcel(): void {
    this.exportErrorReport('excel');
  }

  private exportErrorReport(format: 'csv' | 'excel'): void {
    const data = buildImportErrorReport(this.rows);
    if (!data.length) {
      this.toast.error('No issues to export.');
      return;
    }
    const columns: ExportColumn<ImportErrorReportRow>[] = [
      { header: 'Row', accessor: r => r.Row },
      { header: 'Email', accessor: r => r.Email },
      { header: 'Field', accessor: r => r.Field },
      { header: 'Severity', accessor: r => r.Severity },
      { header: 'Message', accessor: r => r.Message },
      { header: 'Suggestion', accessor: r => r.Suggestion },
      { header: 'ServerResult', accessor: r => r.ServerResult }
    ];
    if (format === 'csv') {
      this.exportService.exportCsv(data, columns, { filename: 'users-import-validation-report' });
    } else {
      this.exportService.exportExcel(data, columns, {
        filename: 'users-import-validation-report',
        sheetName: 'Issues',
        title: 'User import validation report'
      });
    }
  }

  pickFile(): void {
    this.fileInput?.nativeElement.click();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.fileName = file.name;
    this.done = false;

    const reader = new FileReader();
    reader.onload = () => {
      try {
        const data = new Uint8Array(reader.result as ArrayBuffer);
        const book = XLSX.read(data, { type: 'array' });
        const sheet = book.Sheets[book.SheetNames[0]];
        const records = XLSX.utils.sheet_to_json<Record<string, unknown>>(sheet, { defval: '' });
        this.rows = parseUserImportRecords(records, this.parseContext);
        if (!this.rows.length) {
          this.toast.error('No data rows found in the file.');
        }
      } catch {
        this.toast.error('Could not read that file. Use CSV or Excel (.xlsx).');
        this.rows = [];
      }
    };
    reader.readAsArrayBuffer(file);
    input.value = '';
  }

  onRoleFix(row: PreviewRow, code: string): void {
    const idx = this.rows.findIndex(r => r.rowNumber === row.rowNumber);
    if (idx < 0) return;
    const updated = reparseImportRow(row, this.parseContext, { platformRoleCode: code, roleRaw: code });
    this.rows[idx] = { ...updated, roleOverrideCode: code };
    this.rows = [...this.rows];
  }

  submit(): void {
    const users: CreateUserDto[] = this.rows
      .filter(r => this.isRowImportable(r))
      .map(r => {
        const dto = { ...r.dto! };
        if (this.generatePasswords && !dto.password?.trim()) {
          dto.password = '';
        }
        return dto;
      });

    if (!users.length) return;

    const verb = this.dryRun ? 'Validate (dry run)' : 'Create';
    if (!confirm(`${verb} ${users.length} user(s)? Blank passwords will be auto-generated.`)) return;

    this.submitting = true;
    this.userService.bulkCreate({
      users,
      options: {
        dryRun: this.dryRun,
        skipDuplicates: this.skipDuplicates,
        mode: 'CreateOnly'
      }
    }).subscribe({
      next: result => {
        this.submitting = false;
        this.done = true;

        const byEmail = new Map(
          result.created.map(c => [c.email.toLowerCase(), c])
        );
        const errByEmail = new Map(
          result.errors
            .filter(e => !!e.email)
            .map(e => [e.email!.toLowerCase(), e])
        );
        const skipByEmail = new Map(
          (result.skippedRows ?? []).map(s => [s.email.toLowerCase(), s])
        );

        this.rows = this.rows.map(row => {
          if (!row.dto || !this.isRowImportable(row)) return row;
          const key = row.dto.email.toLowerCase();
          const ok = byEmail.get(key);
          if (ok) {
            if (result.dryRun || ok.dryRun) {
              return { ...row, dryRunOk: true, serverError: null, serverSkipped: null };
            }
            return {
              ...row,
              created: true,
              temporaryPassword: ok.temporaryPassword,
              serverError: null,
              serverSkipped: null
            };
          }
          const skip = skipByEmail.get(key);
          if (skip) {
            return { ...row, serverSkipped: skip.reason, serverError: null };
          }
          const fail = errByEmail.get(key);
          if (fail) {
            return { ...row, created: false, serverError: fail.error, serverSkipped: null };
          }
          return {
            ...row,
            created: false,
            serverError: 'No result returned for this row.',
            serverSkipped: null
          };
        });

        if (result.dryRun) {
          this.toast.success(`Dry run: ${result.succeeded} would be created, ${result.failed} failed, ${result.skipped} skipped.`);
        } else if (result.succeeded > 0 && result.failed === 0) {
          this.toast.success(`Created ${result.succeeded} user(s); skipped ${result.skipped}.`);
        } else if (result.succeeded > 0) {
          this.toast.success(`Created ${result.succeeded}; ${result.failed} failed; skipped ${result.skipped}.`);
        } else {
          this.toast.error(`Import failed for ${result.failed} row(s).`);
        }
      },
      error: () => {
        this.submitting = false;
        this.toast.error('Bulk import request failed.');
      }
    });
  }

  close(): void {
    this.dialogRef.close({
      created: this.createdCount,
      failed: this.rows.filter(r => !!r.serverError).length
    });
  }

  cancel(): void {
    this.dialogRef.close({ created: 0, failed: 0 });
  }
}
