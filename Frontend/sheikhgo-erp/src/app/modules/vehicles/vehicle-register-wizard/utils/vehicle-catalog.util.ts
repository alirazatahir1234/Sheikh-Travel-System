import { UiSelectOption } from '../../../../shared/components/ui/types/ui.types';

export const DEFAULT_VEHICLE_NAMES = [
  'Toyota Hiace',
  'Toyota Coaster',
  'Toyota Land Cruiser',
  'Toyota Prado',
  'Nissan Urvan',
  'Nissan Patrol',
  'Mercedes Sprinter',
  'Mercedes Vito',
  'Ford Transit',
  'Hyundai H1',
  'Hyundai Staria',
  'Isuzu NPR',
  'Mitsubishi Rosa',
  'Mitsubishi L300',
  'Kia Carnival',
  'Honda Odyssey',
  'Chevrolet Express',
  'GWM Poer',
  'Land Rover Defender',
  'Lexus LX'
] as const;

export const DEFAULT_VEHICLE_MAKES = [
  'Toyota',
  'Nissan',
  'Mercedes-Benz',
  'Ford',
  'Hyundai',
  'Isuzu',
  'Mitsubishi',
  'Kia',
  'Honda',
  'Chevrolet',
  'BMW',
  'Land Rover',
  'Lexus',
  'GWM',
  'Volkswagen',
  'Peugeot',
  'Renault',
  'Suzuki'
] as const;

export const DEFAULT_VEHICLE_MODELS_BY_MAKE: Record<string, string[]> = {
  Toyota: ['Hiace', 'Coaster', 'Land Cruiser', 'Prado', 'Fortuner', 'Corolla', 'Camry', 'Hilux'],
  Nissan: ['Urvan', 'Patrol', 'Sunny', 'Altima', 'X-Trail', 'Navara'],
  'Mercedes-Benz': ['Sprinter', 'Vito', 'Actros', 'E-Class', 'S-Class'],
  Ford: ['Transit', 'Ranger', 'Explorer', 'Everest'],
  Hyundai: ['H1', 'Staria', 'Sonata', 'Tucson', 'Santa Fe'],
  Isuzu: ['NPR', 'NQR', 'D-Max', 'MU-X'],
  Mitsubishi: ['Rosa', 'L300', 'Pajero', 'Attrage'],
  Kia: ['Carnival', 'Sportage', 'Sorento', 'Picanto'],
  Honda: ['Odyssey', 'Accord', 'Civic', 'CR-V'],
  Chevrolet: ['Express', 'Tahoe', 'Traverse'],
  BMW: ['X5', 'X7', '5 Series', '7 Series'],
  'Land Rover': ['Defender', 'Discovery', 'Range Rover'],
  Lexus: ['LX', 'GX', 'RX', 'ES'],
  GWM: ['Poer', 'Tank 300', 'Haval H6'],
  Volkswagen: ['Transporter', 'Crafter', 'Tiguan'],
  Peugeot: ['Boxer', 'Expert', '3008'],
  Renault: ['Master', 'Trafic', 'Duster'],
  Suzuki: ['Ertiga', 'Jimny', 'Swift']
};

export const DEFAULT_VEHICLE_COLORS = [
  'White',
  'Pearl White',
  'Black',
  'Silver',
  'Grey',
  'Red',
  'Blue',
  'Brown',
  'Beige',
  'Gold',
  'Maroon',
  'Green',
  'Orange',
  'Yellow',
  'Purple',
  'Champagne'
] as const;

function uniqueSorted(values: Iterable<string>): string[] {
  return [...new Set([...values].map(v => v.trim()).filter(Boolean))].sort((a, b) =>
    a.localeCompare(b)
  );
}

export function toSelectOptions(values: Iterable<string>): UiSelectOption[] {
  return uniqueSorted(values).map(value => ({ value, label: value }));
}

export function buildDefaultDetailCatalogOptions(): {
  names: UiSelectOption[];
  makes: UiSelectOption[];
  colors: UiSelectOption[];
  modelsByMake: Record<string, string[]>;
} {
  return {
    names: toSelectOptions(DEFAULT_VEHICLE_NAMES),
    makes: toSelectOptions(DEFAULT_VEHICLE_MAKES),
    colors: toSelectOptions(DEFAULT_VEHICLE_COLORS),
    modelsByMake: Object.fromEntries(
      Object.entries(DEFAULT_VEHICLE_MODELS_BY_MAKE).map(([make, models]) => [make, [...models]])
    )
  };
}

export function modelOptionsForMake(
  make: string | null | undefined,
  modelsByMake: Record<string, string[]>
): UiSelectOption[] {
  const key = String(make ?? '').trim();
  if (!key) {
    const all = uniqueSorted(Object.values(modelsByMake).flat());
    return toSelectOptions(all);
  }
  const models = modelsByMake[key] ?? [];
  return toSelectOptions(models);
}

export function mergeModelsByMake(
  base: Record<string, string[]>,
  make: string,
  model: string
): Record<string, string[]> {
  const mk = make.trim();
  const md = model.trim();
  if (!mk || !md) return base;
  const next = { ...base };
  const list = [...(next[mk] ?? [])];
  if (!list.includes(md)) list.push(md);
  next[mk] = list.sort((a, b) => a.localeCompare(b));
  return next;
}

export function withSelectOption(
  options: UiSelectOption[],
  value: string | null | undefined
): UiSelectOption[] {
  const v = String(value ?? '').trim();
  if (!v || options.some(o => o.value === v)) return options;
  return [{ value: v, label: v }, ...options];
}
