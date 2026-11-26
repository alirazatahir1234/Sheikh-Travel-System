import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject, catchError, of, switchMap, tap } from 'rxjs';
import { UiEmptyStateComponent, UiPageHeaderComponent, UiStatusBadgeComponent, UiTab, UiTabsComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { statusFromQuery } from '../../models/travel-date.util';
import {
  REMINDER_AUDIENCE_LABEL, REMINDER_ENTITY_ROUTE, REMINDER_KIND_LABEL, REMINDER_STATUS_VARIANT, TRAVEL_PERMISSIONS, TRAVEL_REMINDER_AUDIENCES,
  TRAVEL_REMINDER_RULE_TYPES, TRAVEL_REMINDER_STATUSES, TravelReminderAudience, TravelReminderLead, TravelReminderLogItem, TravelReminderRule,
  TravelReminderRuleType, TravelReminderStatus
} from '../../models/travel.models';
import { TravelReminderService } from '../../services/travel-reminder.service';

/** A rule being edited: its lead rows and customer text. */
export interface RuleDraft { leads: TravelReminderLead[]; customerTemplate: string; }

/** Adds a lead time once, ordered; returns null when it is already there or out of range. */
export function addLead(leads: TravelReminderLead[], leadDays: number, supportsStaff: boolean, supportsCustomer: boolean): TravelReminderLead[] | null {
  if (!Number.isInteger(leadDays) || leadDays < 0 || leadDays > 365 || leads.some(l => l.leadDays === leadDays)) return null;
  const row: TravelReminderLead = { leadDays, isEnabled: true, staffInApp: supportsStaff, staffEmail: false, customerEmail: supportsCustomer, customerSms: false, customerWhatsApp: false };
  return [...leads, row].sort((a, b) => a.leadDays - b.leadDays);
}

/** What stops a rule draft from being saved. */
export function draftProblem(d: RuleDraft, kind: string): string | null {
  if (!d.leads.length) return 'Add at least one lead time.';
  if (d.leads.length > 6) return 'At most 6 lead times.';
  if (kind !== 'Upcoming' && d.leads.some(l => l.leadDays !== 0)) return 'This rule fires on the day; use a lead time of 0.';
  if (d.customerTemplate.length > 1000) return 'The customer text is too long (1000 characters).';
  return null;
}

@Component({
  selector: 'app-travel-reminders-page',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiTabsComponent, UiStatusBadgeComponent, UiEmptyStateComponent,
    HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="Reminders" eyebrow="TravelNexus" icon="notifications_active"
        subtitle="Built-in rules you tune: when they fire, who gets them (your agents in-app or by email, customers by email, SMS or WhatsApp) and what customers read."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Reminders' }]">
        <button mat-flat-button color="primary" *appHasPermission="perms.reminderManage" [disabled]="running()" (click)="runNow()"><mat-icon>play_arrow</mat-icon> {{ running() ? 'Running…' : 'Run now' }}</button>
      </ui-page-header>
      @if (error()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}</div> }
      <div class="tn-banner info"><mat-icon>info</mat-icon>
        <span>Reminders run every 30 minutes for every company. Each reminder is sent once per trigger date, lead time and audience. Customer messages wait
          through the quiet hours set under <a class="underline" routerLink="/travel/settings" [queryParams]="{ tab: 'general' }">Settings → General</a>.
          WhatsApp to a customer who has not written to you in the last 24 hours needs your approved Meta template.</span></div>

      <ui-tabs [tabs]="tabs" [(activeId)]="active"></ui-tabs>
      @switch (active()) {
        @case ('rules') {
          @if (!rules().length) { <div class="tn-card"><ui-empty-state icon="rule" title="Loading rules…" description=""></ui-empty-state></div> }
          <div class="grid gap-4 lg:grid-cols-2">
            @for (r of rules(); track r.ruleType) {
              <div class="tn-card tn-card-body space-y-3" [class.opacity-70]="!isOn(r)">
                <div class="flex items-start justify-between gap-2">
                  <div><h3 class="tn-section-title">{{ r.name }}</h3>
                    <p class="text-xs text-fleet-text-muted">{{ r.description }} <span class="text-fleet-text-soft">· {{ kindLabel[r.kind] }}{{ r.isCustomised ? '' : ' · defaults' }}</span></p></div>
                  <ui-status-badge [status]="isOn(r) ? 'active' : 'inactive'" [label]="isOn(r) ? 'On' : 'Off'"></ui-status-badge>
                </div>
                <table class="w-full text-sm">
                  <thead><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                    <th class="py-1 text-left">{{ r.kind === 'Upcoming' ? 'Days before' : 'When' }}</th><th class="py-1">On</th>
                    @if (r.supportsStaff) { <th class="py-1">Agent in-app</th><th class="py-1">Agent email</th> }
                    @if (r.supportsCustomer) { <th class="py-1">Cust. email</th><th class="py-1">Cust. SMS</th><th class="py-1">Cust. WhatsApp</th> }
                    <th class="py-1"></th></tr></thead>
                  <tbody>
                    @for (l of draft(r).leads; track l.leadDays; let i = $index) {
                      <tr class="border-t border-fleet-border text-center">
                        <td class="py-1 text-left font-semibold">{{ r.kind === 'Upcoming' ? l.leadDays + ' day(s)' : (r.kind === 'Overdue' ? 'while overdue' : 'on change') }}</td>
                        <td><input type="checkbox" [attr.aria-label]="r.name + ' enabled'" [(ngModel)]="l.isEnabled" (ngModelChange)="touch(r)" /></td>
                        @if (r.supportsStaff) {
                          <td><input type="checkbox" aria-label="Agent in-app" [(ngModel)]="l.staffInApp" (ngModelChange)="touch(r)" /></td>
                          <td><input type="checkbox" aria-label="Agent email" [(ngModel)]="l.staffEmail" (ngModelChange)="touch(r)" /></td>
                        }
                        @if (r.supportsCustomer) {
                          <td><input type="checkbox" aria-label="Customer email" [(ngModel)]="l.customerEmail" (ngModelChange)="touch(r)" /></td>
                          <td><input type="checkbox" aria-label="Customer SMS" [(ngModel)]="l.customerSms" (ngModelChange)="touch(r)" /></td>
                          <td><input type="checkbox" aria-label="Customer WhatsApp" [(ngModel)]="l.customerWhatsApp" (ngModelChange)="touch(r)" /></td>
                        }
                        <td>@if (r.kind === 'Upcoming' && draft(r).leads.length > 1) {
                          <button mat-icon-button *appHasPermission="perms.reminderManage" aria-label="Remove lead time" (click)="removeLead(r, i)"><mat-icon>close</mat-icon></button> }</td>
                      </tr>
                    }
                  </tbody>
                </table>
                @if (r.kind === 'Upcoming') {
                  <div class="flex items-center gap-2 text-xs" *appHasPermission="perms.reminderManage">
                    <label>Add lead <input type="number" min="0" max="365" class="tn-input inline w-20" [attr.aria-label]="'New lead days for ' + r.name" [(ngModel)]="newLead[r.ruleType]" /> days</label>
                    <button mat-button type="button" (click)="addLeadRow(r)">Add</button>
                  </div>
                }
                @if (r.supportsCustomer) {
                  <div class="tn-field"><label [for]="'tpl-' + r.ruleType">Customer text (optional)</label>
                    <textarea [id]="'tpl-' + r.ruleType" class="tn-input" rows="2" maxlength="1000" [(ngModel)]="draft(r).customerTemplate" (ngModelChange)="touch(r)"
                      placeholder="Leave empty for the built-in wording"></textarea>
                    <span class="tn-hint">Placeholders: {{ r.placeholders.join(' ') }}</span></div>
                }
                @if (problem(r)) { <p class="text-xs text-red-600">{{ problem(r) }}</p> }
                <div class="tn-actions" *appHasPermission="perms.reminderManage">
                  @if (dirty().has(r.ruleType)) { <button mat-stroked-button (click)="revert(r)">Revert</button> }
                  <button mat-flat-button color="primary" [disabled]="!dirty().has(r.ruleType) || !!problem(r) || saving().has(r.ruleType)" (click)="save(r)">
                    {{ saving().has(r.ruleType) ? 'Saving…' : 'Save' }}</button>
                </div>
              </div>
            }
          </div>
        }
        @case ('log') {
          <div class="tn-card">
            <div class="flex flex-col gap-3 border-b border-fleet-border px-5 py-4 lg:flex-row lg:flex-wrap lg:items-end">
              <select class="tn-input lg:w-48" aria-label="Rule" [ngModel]="ruleType()" (ngModelChange)="ruleType.set($event); reset()">
                <option value="">All rules</option>@for (t of ruleTypes; track t) { <option [value]="t">{{ ruleName(t) }}</option> }</select>
              <select class="tn-input lg:w-36" aria-label="Status" [ngModel]="status()" (ngModelChange)="status.set($event); reset()">
                <option value="">All statuses</option>@for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }</select>
              <select class="tn-input lg:w-36" aria-label="Audience" [ngModel]="audience()" (ngModelChange)="audience.set($event); reset()">
                <option value="">Staff and customers</option>@for (a of audiences; track a) { <option [value]="a">{{ audienceLabel[a] }}</option> }</select>
              <label class="tn-field lg:w-40"><span class="text-xs">From</span><input type="date" class="tn-input" [ngModel]="from()" (ngModelChange)="from.set($event); reset()" /></label>
              <label class="tn-field lg:w-40"><span class="text-xs">To</span><input type="date" class="tn-input" [ngModel]="to()" (ngModelChange)="to.set($event); reset()" /></label>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full min-w-[960px] text-left text-sm">
                <thead class="bg-fleet-surface-alt/60"><tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                  <th class="px-4 py-3">Sent</th><th class="px-4 py-3">Rule</th><th class="px-4 py-3">About</th><th class="px-4 py-3">To</th><th class="px-4 py-3">Channels</th><th class="px-4 py-3">Status</th></tr></thead>
                <tbody class="divide-y divide-fleet-border">
                  @if (loading()) { <tr><td colspan="6" class="px-4 py-6 text-center text-fleet-text-muted">Loading…</td></tr> }
                  @else if (!rows().length) { <tr><td colspan="6"><ui-empty-state icon="notifications_none" title="No reminders yet" description="Reminders appear here as they are sent, skipped or fail."></ui-empty-state></td></tr> }
                  @for (r of rows(); track r.id) {
                    <tr>
                      <td class="px-4 py-3 whitespace-nowrap">{{ r.createdAt | date: 'd MMM, HH:mm' }}</td>
                      <td class="px-4 py-3">{{ ruleName(r.ruleType) }}@if (r.leadDays) { <span class="text-xs text-fleet-text-muted"> · {{ r.leadDays }} d before</span> }
                        <span class="block text-xs text-fleet-text-muted">due {{ r.dueAt | date: 'd MMM yyyy' }}</span></td>
                      <td class="px-4 py-3"><a class="text-fleet-primary underline" [routerLink]="entityLink(r)">{{ r.contextLabel }}</a>
                        <span class="block max-w-md truncate text-xs text-fleet-text-muted" [title]="r.body">{{ r.title }}</span></td>
                      <td class="px-4 py-3">{{ r.audience === 'Staff' ? (r.recipientUserName || 'Agent') : (r.recipientCustomerName || 'Customer') }}
                        <span class="block text-xs text-fleet-text-muted">{{ audienceLabel[r.audience] }}</span></td>
                      <td class="px-4 py-3 text-xs">{{ r.channels || '—' }}</td>
                      <td class="px-4 py-3"><ui-status-badge [status]="r.status" [variant]="statusVariant[r.status]"></ui-status-badge>
                        @if (r.failureReason) { <span class="block max-w-xs text-xs text-fleet-text-muted">{{ r.failureReason }}</span> }</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            @if (!loading() && totalPages() > 1) {
              <div class="flex items-center justify-between border-t border-fleet-border px-5 py-3 text-sm">
                <span class="text-fleet-text-muted">Page {{ page() }} of {{ totalPages() }}</span>
                <div class="flex gap-2"><button mat-stroked-button [disabled]="page() <= 1" (click)="goTo(page() - 1)">Previous</button>
                  <button mat-stroked-button [disabled]="page() >= totalPages()" (click)="goTo(page() + 1)">Next</button></div>
              </div>
            }
          </div>
        }
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelRemindersPageComponent {
  private readonly api = inject(TravelReminderService);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly kindLabel = REMINDER_KIND_LABEL;
  protected readonly audienceLabel = REMINDER_AUDIENCE_LABEL;
  protected readonly statusVariant = REMINDER_STATUS_VARIANT;
  protected readonly ruleTypes = TRAVEL_REMINDER_RULE_TYPES;
  protected readonly statuses = TRAVEL_REMINDER_STATUSES;
  protected readonly audiences = TRAVEL_REMINDER_AUDIENCES;
  protected readonly tabs: UiTab[] = [{ id: 'rules', label: 'Rules', icon: 'rule' }, { id: 'log', label: 'Log', icon: 'history' }];
  protected readonly active = signal(this.route.snapshot.queryParamMap.has('status') ? 'log' : 'rules');
  protected readonly error = signal<string | null>(null);
  protected readonly running = signal(false);

  protected readonly rules = signal<TravelReminderRule[]>([]);
  protected readonly dirty = signal(new Set<TravelReminderRuleType>());
  protected readonly saving = signal(new Set<TravelReminderRuleType>());
  private readonly drafts = new Map<TravelReminderRuleType, RuleDraft>();
  protected newLead: Partial<Record<TravelReminderRuleType, number | null>> = {};

  protected readonly rows = signal<TravelReminderLogItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly ruleType = signal<TravelReminderRuleType | ''>('');
  protected readonly status = signal<TravelReminderStatus | ''>(statusFromQuery(this.route.snapshot.queryParamMap.get('status'), TRAVEL_REMINDER_STATUSES));
  protected readonly audience = signal<TravelReminderAudience | ''>('');
  protected readonly from = signal('');
  protected readonly to = signal('');
  private readonly load$ = new Subject<void>();

  constructor() {
    this.loadRules();
    this.load$.pipe(
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(() => this.api.getLog({ page: this.page(), pageSize: 25, ruleType: this.ruleType(), status: this.status(), audience: this.audience(),
        from: this.from() || undefined, to: this.to() || undefined })
        .pipe(catchError(err => { this.error.set(apiErrorMessage(err, 'Could not load the reminder log.')); return of(null); }))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(r => { if (r) { this.rows.set(r.items); this.totalPages.set(r.totalPages); } this.loading.set(false); });
    effect(() => { if (this.active() === 'log') untracked(() => this.reset()); });
  }

  private loadRules(): void {
    this.api.getRules().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: rules => { this.drafts.clear(); this.dirty.set(new Set()); this.rules.set(rules); },
      error: err => this.error.set(apiErrorMessage(err, 'Could not load reminder rules.'))
    });
  }

  protected draft(r: TravelReminderRule): RuleDraft {
    let d = this.drafts.get(r.ruleType);
    if (!d) { d = { leads: r.leads.map(l => ({ ...l })), customerTemplate: r.customerTemplate ?? '' }; this.drafts.set(r.ruleType, d); }
    return d;
  }
  protected isOn(r: TravelReminderRule): boolean { return this.draft(r).leads.some(l => l.isEnabled && (l.staffInApp || l.staffEmail || l.customerEmail || l.customerSms || l.customerWhatsApp)); }
  protected problem(r: TravelReminderRule): string | null { return draftProblem(this.draft(r), r.kind); }
  protected touch(r: TravelReminderRule): void { this.dirty.update(s => new Set(s).add(r.ruleType)); }
  protected revert(r: TravelReminderRule): void { this.drafts.delete(r.ruleType); this.dirty.update(s => { const n = new Set(s); n.delete(r.ruleType); return n; }); }

  protected addLeadRow(r: TravelReminderRule): void {
    const d = this.draft(r);
    const next = addLead(d.leads, Number(this.newLead[r.ruleType]), r.supportsStaff, r.supportsCustomer);
    if (!next) { this.toast.warning('Enter a whole number of days (0–365) that is not already listed.'); return; }
    d.leads = next;
    this.newLead[r.ruleType] = null;
    this.touch(r);
  }
  protected removeLead(r: TravelReminderRule, i: number): void { const d = this.draft(r); d.leads = d.leads.filter((_, j) => j !== i); this.touch(r); }

  protected save(r: TravelReminderRule): void {
    const d = this.draft(r);
    this.saving.update(s => new Set(s).add(r.ruleType));
    this.api.saveRule(r.ruleType, { leads: d.leads, customerTemplate: d.customerTemplate.trim() || null }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: saved => {
        this.saving.update(s => { const n = new Set(s); n.delete(r.ruleType); return n; });
        this.drafts.delete(r.ruleType);
        this.dirty.update(s => { const n = new Set(s); n.delete(r.ruleType); return n; });
        this.rules.update(list => list.map(x => x.ruleType === saved.ruleType ? saved : x));
        this.toast.success(`${r.name} saved.`);
      },
      error: err => { this.saving.update(s => { const n = new Set(s); n.delete(r.ruleType); return n; }); this.toast.error(apiErrorMessage(err, 'Could not save the rule.')); }
    });
  }

  protected runNow(): void {
    this.running.set(true);
    this.api.run().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => {
        this.running.set(false);
        this.toast.success(`Reminders run: ${r.sent} sent, ${r.skipped} skipped, ${r.failed} failed${r.held ? `, ${r.held} held for quiet hours` : ''}.`);
        this.active.set('log');
        this.reset();
      },
      error: err => { this.running.set(false); this.toast.error(apiErrorMessage(err, 'Could not run reminders.')); }
    });
  }

  protected reset(): void { this.page.set(1); this.load$.next(); }
  protected goTo(p: number): void { this.page.set(p); this.load$.next(); }
  protected ruleName(t: TravelReminderRuleType): string { return this.rules().find(r => r.ruleType === t)?.name ?? t.replace(/([a-z])([A-Z])/g, '$1 $2'); }
  protected entityLink(r: TravelReminderLogItem): string[] {
    return r.entityType === 'FlightBooking' || r.entityType === 'HotelBooking' || r.entityType === 'Traveler' ? [REMINDER_ENTITY_ROUTE[r.entityType]] : [REMINDER_ENTITY_ROUTE[r.entityType], String(r.entityId)];
  }
}
