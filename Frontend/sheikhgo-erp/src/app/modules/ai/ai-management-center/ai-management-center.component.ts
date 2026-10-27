import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { forkJoin, of } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import {
  AiPlatformService,
  AiCopilotResponse,
  AiPrediction,
  AiProviderConfig,
  AiRecommendation,
  EscalationPending,
  EscalationRule,
  FleetHealth
} from '../../../core/services/ai-platform.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { AiChatComponent } from '../ai-chat/ai-chat.component';
import { healthBand } from '../shared/assistant-answer.util';

@Component({
  selector: 'app-ai-management-center',
  standalone: true,
  imports: [
    CommonModule, // NgClass, NgFor, etc.
    FormsModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
    MatSelectModule,
    MatTabsModule,
    MatProgressSpinnerModule,
    AiChatComponent
  ],
  templateUrl: './ai-management-center.component.html',
  styleUrls: ['./ai-management-center.component.scss']
})
export class AiManagementCenterComponent implements OnInit {
  /** Management data load only — Chat tab is never gated on this. */
  loading = true;
  loadError: string | null = null;
  health: FleetHealth | null = null;
  recommendations: AiRecommendation[] = [];
  predictions: AiPrediction[] = [];
  datasets: { name: string; rowCount: number; lastCapturedAt?: string; freshness: string }[] = [];
  config: AiProviderConfig = {
    provider: 'None',
    isEnabled: false,
    copilotEnabled: false,
    decisionEngineEnabled: true,
    digestEnabled: true,
    predictionsEnabled: true
  };
  rules: EscalationRule[] = [];
  pending: EscalationPending[] = [];
  question = '';
  copilotAnswer: AiCopilotResponse | null = null;
  asking = false;

  constructor(
    private ai: AiPlatformService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.loadError = null;

    forkJoin({
      health: this.ai.getHealth().pipe(catchError(() => of(null))),
      recommendations: this.ai.getRecommendations().pipe(catchError(() => of([] as AiRecommendation[]))),
      predictions: this.ai.getPredictions().pipe(catchError(() => of([] as AiPrediction[]))),
      datasets: this.ai.getDatasets().pipe(catchError(() => of([] as { name: string; rowCount: number; lastCapturedAt?: string; freshness: string }[]))),
      config: this.ai.getConfig().pipe(
        catchError(() =>
          of({
            provider: 'None',
            isEnabled: false,
            copilotEnabled: false,
            decisionEngineEnabled: true,
            digestEnabled: true,
            predictionsEnabled: true
          } as AiProviderConfig)
        )
      ),
      rules: this.ai.getEscalationRules().pipe(catchError(() => of([] as EscalationRule[]))),
      pending: this.ai.getPendingEscalations().pipe(catchError(() => of([] as EscalationPending[])))
    })
      .pipe(finalize(() => { this.loading = false; }))
      .subscribe({
        next: result => {
          this.health = result.health;
          this.recommendations = result.recommendations ?? [];
          this.predictions = result.predictions ?? [];
          this.datasets = result.datasets ?? [];
          this.config = { ...result.config };
          this.rules = result.rules ?? [];
          this.pending = result.pending ?? [];
          if (!result.health) {
            this.loadError =
              'Some AI management data could not be loaded. Chat still works if the AI module is enabled.';
          }
        },
        error: () => {
          this.loadError = 'Failed to load AI Management Center. Try Refresh, or open the Chat tab.';
        }
      });
  }

  saveConfig(): void {
    this.ai.saveConfig(this.config).subscribe({
      next: c => {
        this.config = c;
        this.toast.success('AI configuration saved');
      },
      error: () => this.toast.error('Failed to save AI config')
    });
  }

  onProviderChange(): void {
    if (this.config.provider === 'DeepInfra') {
      this.config.apiEndpoint = this.config.apiEndpoint || 'https://api.deepinfra.com/v1/openai';
      this.config.modelName = this.config.modelName || 'Qwen/Qwen3-32B';
      this.config.isEnabled = true;
      this.config.copilotEnabled = true;
    } else if (this.config.provider === 'Ollama') {
      this.config.apiEndpoint = this.config.apiEndpoint || 'http://127.0.0.1:11434';
      this.config.modelName = this.config.modelName || 'qwen3:8b';
      this.config.isEnabled = true;
      this.config.copilotEnabled = true;
    }
  }

  ask(): void {
    if (!this.question.trim()) return;
    this.asking = true;
    this.ai.ask(this.question.trim()).subscribe({
      next: a => {
        this.copilotAnswer = a;
        this.asking = false;
      },
      error: () => {
        this.asking = false;
        this.toast.error('Copilot request failed');
      }
    });
  }

  runPredictions(): void {
    this.ai.runPredictions().subscribe({
      next: p => {
        this.predictions = p;
        this.toast.success('Predictions refreshed');
      },
      error: () => this.toast.error('Prediction run failed')
    });
  }

  generateDigest(): void {
    this.ai.generateDigest().subscribe({
      next: () => this.toast.success('Morning digest queued'),
      error: () => this.toast.error('Digest generation failed')
    });
  }

  ack(id: number): void {
    this.ai.ackEscalation(id).subscribe({
      next: () => {
        this.pending = this.pending.filter(p => p.id !== id);
        this.toast.success('Escalation acknowledged');
      },
      error: () => this.toast.error('Ack failed')
    });
  }

  saveRule(rule: EscalationRule): void {
    this.ai.saveEscalationRule(rule).subscribe({
      next: () => this.toast.success('Escalation rule saved'),
      error: () => this.toast.error('Failed to save rule')
    });
  }

  dismissRec(rec: AiRecommendation): void {
    this.ai.recordLearning(rec.category, 'Ignore').subscribe({
      next: () => {
        this.recommendations = this.recommendations.filter(r => r.id !== rec.id);
        this.toast.success('Recommendation dismissed');
      }
    });
  }

  healthBandClass(percent: number): string {
    return healthBand(percent);
  }

  severityClass(severity: string): string {
    const s = (severity ?? '').toLowerCase();
    if (s === 'critical') return 'sev-critical';
    if (s === 'high') return 'sev-high';
    if (s === 'medium' || s === 'warning') return 'sev-medium';
    return 'sev-info';
  }

  predictionBand(probability: number): 'high' | 'medium' | 'low' {
    if (probability >= 70) return 'high';
    if (probability >= 40) return 'medium';
    return 'low';
  }

  freshnessTone(freshness: string): string {
    const f = (freshness ?? '').toLowerCase();
    if (f.includes('stale') || f.includes('old')) return 'stale';
    if (f.includes('fresh') || f.includes('current')) return 'fresh';
    return 'neutral';
  }
}
