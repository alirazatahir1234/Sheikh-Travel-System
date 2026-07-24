import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { PlatformService } from '../../../core/services/platform.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import {
  GpsCapability,
  GpsCommandDefinition,
  GpsCommandTemplate,
  GpsControlDashboard,
  GpsManufacturer,
  GpsSimulateResult,
  GpsTrackerModel
} from '../../../core/models/platform.model';

type TabId = 'dashboard' | 'manufacturers' | 'models' | 'capabilities' | 'commands' | 'templates' | 'console';

@Component({
  standalone: false,
  selector: 'app-gps-control-center',
  templateUrl: './gps-control-center.component.html',
  styleUrls: ['./gps-control-center.component.scss']
})
export class GpsControlCenterComponent implements OnInit, OnDestroy {
  readonly tabs: { id: TabId; label: string }[] = [
    { id: 'dashboard', label: 'Dashboard' },
    { id: 'manufacturers', label: 'Manufacturers' },
    { id: 'models', label: 'Models' },
    { id: 'capabilities', label: 'Capabilities' },
    { id: 'commands', label: 'Commands' },
    { id: 'templates', label: 'Templates' },
    { id: 'console', label: 'Testing Console' }
  ];

  activeTab: TabId = 'dashboard';
  loading = false;

  dashboard: GpsControlDashboard | null = null;
  manufacturers: GpsManufacturer[] = [];
  models: GpsTrackerModel[] = [];
  capabilities: GpsCapability[] = [];
  commands: GpsCommandDefinition[] = [];
  templates: GpsCommandTemplate[] = [];
  modelCapKeys: string[] = [];

  selectedModelId: number | null = null;
  brandForm: GpsManufacturer = this.emptyBrand();
  modelForm: GpsTrackerModel = this.emptyModel();
  templateForm: GpsCommandTemplate = this.emptyTemplate();

  consoleModelId: number | null = null;
  consoleCommandKey = 'engineStop';
  consoleFirmware = '';
  consolePreview: string | null = null;
  consoleSimulate: GpsSimulateResult | null = null;

  private readonly destroy$ = new Subject<void>();

  constructor(
    private platform: PlatformService,
    private toast: UiToastService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadDashboard();
    this.loadManufacturers();
    this.loadModels();
    this.loadCapabilities();
    this.loadCommands();
    this.loadTemplates();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  setTab(tab: TabId): void {
    this.activeTab = tab;
  }

  back(): void {
    void this.router.navigate(['/platform']);
  }

  loadDashboard(): void {
    this.platform.getGpsControlDashboard().pipe(takeUntil(this.destroy$)).subscribe({
      next: d => (this.dashboard = d),
      error: () => this.toast.error('Failed to load GPS dashboard')
    });
  }

  loadManufacturers(): void {
    this.platform.getGpsManufacturers().pipe(takeUntil(this.destroy$)).subscribe({
      next: rows => (this.manufacturers = rows),
      error: () => this.toast.error('Failed to load manufacturers')
    });
  }

  loadModels(): void {
    this.platform.getGpsTrackerModels().pipe(takeUntil(this.destroy$)).subscribe({
      next: rows => {
        this.models = rows;
        if (!this.selectedModelId && rows.length) {
          this.selectedModelId = rows[0].id;
          this.loadModelCaps();
        }
        if (!this.consoleModelId && rows.length) this.consoleModelId = rows[0].id;
      },
      error: () => this.toast.error('Failed to load models')
    });
  }

  loadCapabilities(): void {
    this.platform.getGpsCapabilities().pipe(takeUntil(this.destroy$)).subscribe({
      next: rows => (this.capabilities = rows)
    });
  }

  loadCommands(): void {
    this.platform.getGpsCommandDefinitions().pipe(takeUntil(this.destroy$)).subscribe({
      next: rows => {
        this.commands = rows;
        if (rows.length && !this.consoleCommandKey) this.consoleCommandKey = rows[0].commandKey;
      }
    });
  }

  loadTemplates(): void {
    this.platform.getGpsCommandTemplates().pipe(takeUntil(this.destroy$)).subscribe({
      next: rows => (this.templates = rows)
    });
  }

  loadModelCaps(): void {
    if (!this.selectedModelId) return;
    this.platform.getGpsModelCapabilities(this.selectedModelId).pipe(takeUntil(this.destroy$)).subscribe({
      next: keys => (this.modelCapKeys = keys)
    });
  }

  editBrand(b: GpsManufacturer): void {
    this.brandForm = { ...b };
  }

  saveBrand(): void {
    this.platform.upsertGpsManufacturer(this.brandForm).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toast.success('Manufacturer saved');
        this.brandForm = this.emptyBrand();
        this.loadManufacturers();
        this.loadDashboard();
      },
      error: () => this.toast.error('Save failed')
    });
  }

  editModel(m: GpsTrackerModel): void {
    this.modelForm = { ...m };
  }

  saveModel(): void {
    if (!this.modelForm.protocolLabel) this.modelForm.protocolLabel = this.modelForm.protocol;
    this.platform.upsertGpsTrackerModel(this.modelForm).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toast.success('Model saved');
        this.modelForm = this.emptyModel();
        this.loadModels();
        this.loadDashboard();
      },
      error: () => this.toast.error('Save failed')
    });
  }

  toggleCapability(capKey: string, enabled: boolean): void {
    if (!this.selectedModelId) return;
    this.platform.setGpsModelCapability(this.selectedModelId, capKey, enabled).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => this.loadModelCaps(),
      error: () => this.toast.error('Capability update failed')
    });
  }

  hasCap(key: string): boolean {
    return this.modelCapKeys.includes(key);
  }

  editTemplate(t: GpsCommandTemplate): void {
    this.templateForm = { ...t };
  }

  saveTemplate(): void {
    this.platform.upsertGpsCommandTemplate(this.templateForm).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toast.success('Template saved');
        this.templateForm = this.emptyTemplate();
        this.loadTemplates();
        this.loadDashboard();
      },
      error: () => this.toast.error('Save failed')
    });
  }

  previewTranslate(): void {
    if (!this.consoleModelId) return;
    this.platform
      .translateGpsCommand({
        trackerModelId: this.consoleModelId,
        commandKey: this.consoleCommandKey,
        firmwareVersion: this.consoleFirmware || undefined
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: r => {
          this.consolePreview = `${r.transport}: ${r.renderedPayload}`;
          this.toast.success('Translated');
        },
        error: err => this.toast.error(err?.error?.message || 'Translate failed')
      });
  }

  runSimulate(): void {
    if (!this.consoleModelId) return;
    this.platform
      .simulateGpsCommand({
        trackerModelId: this.consoleModelId,
        commandKey: this.consoleCommandKey,
        firmwareVersion: this.consoleFirmware || undefined,
        useSimulator: true
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: r => {
          this.consoleSimulate = r;
          this.consolePreview = r.translation.renderedPayload;
          this.toast.success('Simulation complete');
        },
        error: err => this.toast.error(err?.error?.message || 'Simulate failed')
      });
  }

  private emptyBrand(): GpsManufacturer {
    return {
      id: 0,
      name: '',
      vendorKey: '',
      website: '',
      description: '',
      defaultProtocol: '',
      supportsTraccar: true,
      supportsSms: true,
      isActive: true
    };
  }

  private emptyModel(): GpsTrackerModel {
    return {
      id: 0,
      trackerBrandId: this.manufacturers[0]?.id ?? 0,
      brandName: '',
      name: '',
      catalogKey: '',
      protocol: 'gt06',
      protocolLabel: 'GT06',
      defaultPort: 5023,
      firmwareHint: '',
      supportsEngineCutOff: false,
      supportsRelay: false,
      isActive: true
    };
  }

  private emptyTemplate(): GpsCommandTemplate {
    return {
      id: 0,
      trackerModelId: this.models[0]?.id ?? 0,
      modelName: '',
      commandKey: 'engineStop',
      transport: 'Traccar',
      payloadTemplate: '',
      traccarType: '',
      parserKey: '',
      firmwareMin: '',
      firmwareMax: '',
      templateVersion: 1,
      isActive: true
    };
  }
}
