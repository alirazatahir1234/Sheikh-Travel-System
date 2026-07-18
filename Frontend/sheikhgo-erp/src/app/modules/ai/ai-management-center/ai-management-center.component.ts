import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
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

@Component({
  selector: 'app-ai-management-center',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
    MatSelectModule,
    MatTabsModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './ai-management-center.component.html',
  styleUrls: ['./ai-management-center.component.scss']
})
export class AiManagementCenterComponent implements OnInit {
  loading = true;
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
    this.ai.getHealth().subscribe({
      next: h => (this.health = h),
      error: () => this.toast.error('Failed to load fleet health')
    });
    this.ai.getRecommendations().subscribe({
      next: r => (this.recommendations = r),
      error: () => {}
    });
    this.ai.getPredictions().subscribe({
      next: p => (this.predictions = p),
      error: () => {}
    });
    this.ai.getDatasets().subscribe({
      next: d => (this.datasets = d),
      error: () => {}
    });
    this.ai.getConfig().subscribe({
      next: c => (this.config = { ...c }),
      error: () => {}
    });
    this.ai.getEscalationRules().subscribe({
      next: r => (this.rules = r),
      error: () => {}
    });
    this.ai.getPendingEscalations().subscribe({
      next: p => {
        this.pending = p;
        this.loading = false;
      },
      error: () => (this.loading = false)
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
}
