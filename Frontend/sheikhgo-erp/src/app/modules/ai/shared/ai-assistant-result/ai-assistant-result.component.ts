import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import {
  AssistantDataTable,
  AssistantFleetHealthData,
  AssistantMetricTile,
  extractDataTable,
  fleetHealthMetrics,
  formatToolLabel,
  healthBand,
  parseAssistantAnswer,
  asFleetHealthData
} from '../assistant-answer.util';
import { polishAssistantEmojis } from '../ai-assistant-ui.constants';

@Component({
  selector: 'app-ai-assistant-result',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './ai-assistant-result.component.html',
  styleUrls: ['./ai-assistant-result.component.scss']
})
export class AiAssistantResultComponent {
  @Input() answer = '';
  @Input() toolUsed?: string | null;
  @Input() data?: unknown;
  @Input() warnings: string[] = [];
  @Input() mode?: string;

  get blocks() {
    return parseAssistantAnswer(this.answer);
  }

  get toolLabel(): string | null {
    return formatToolLabel(this.toolUsed);
  }

  get fleetHealth(): AssistantFleetHealthData | null {
    return asFleetHealthData(this.data);
  }

  get fleetMetrics(): AssistantMetricTile[] {
    const h = this.fleetHealth;
    return h ? fleetHealthMetrics(h) : [];
  }

  get fleetBand(): 'good' | 'fair' | 'poor' | null {
    const h = this.fleetHealth;
    return h ? healthBand(h.healthPercent) : null;
  }

  get table(): AssistantDataTable | null {
    if (this.fleetHealth) return null;
    return extractDataTable(this.data, this.toolUsed);
  }

  metricPercent(m: AssistantMetricTile): number {
    if (m.unit !== '%') return 0;
    return Math.max(0, Math.min(100, m.value));
  }

  polishedWarning(text: string): string {
    return polishAssistantEmojis(text);
  }
}
