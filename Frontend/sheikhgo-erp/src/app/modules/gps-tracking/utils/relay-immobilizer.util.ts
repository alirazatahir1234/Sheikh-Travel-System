import { UiSelectOption } from '../../../shared/components/ui/types/ui.types';
import { TrackerModel } from '../../../core/models/tracker-catalog.model';

export const RELAY_PURPOSE_ENGINE = 'EngineImmobilizer';

export const RELAY_OUTPUT_VALUES = ['output1', 'output2', 'output3', 'output4'] as const;

const RELAY_OUTPUT_LABELS: Record<string, string> = {
  output1: 'Output 1',
  output2: 'Output 2',
  output3: 'Output 3',
  output4: 'Output 4',
};

export const RELAY_OUTPUT_HINT =
  'Select the relay connected to the vehicle\'s engine immobilizer. This output is used when sending remote engine cut-off commands.';

export function resolveDefaultRelayOutput(model?: Pick<TrackerModel, 'defaultRelayOutput' | 'catalogKey' | 'name'> | null): string {
  if (model?.defaultRelayOutput) {
    return model.defaultRelayOutput;
  }

  const key = model?.catalogKey?.toLowerCase() ?? '';
  const name = model?.name?.toLowerCase() ?? '';
  if (key.includes('vg03') || name === 'vg03') {
    return 'output2';
  }

  return 'output1';
}

export function buildRelayOutputOptions(recommended?: string): UiSelectOption[] {
  return RELAY_OUTPUT_VALUES.map(value => ({
    value,
    label: value === recommended
      ? `${RELAY_OUTPUT_LABELS[value]} (Recommended)`
      : RELAY_OUTPUT_LABELS[value],
  }));
}

export function relayOutputLabel(value?: string | null): string {
  if (!value) return '—';
  return RELAY_OUTPUT_LABELS[value] ?? value;
}
