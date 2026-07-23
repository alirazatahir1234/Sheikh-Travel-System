export interface Route {
  id: number;
  name?: string | null;
  source: string;
  destination: string;
  distance: number;
  estimatedMinutes?: number | null;
  basePrice: number;
  isActive: boolean;
  createdAt: string;
  waypointsJson?: string | null;
  optimizeMode?: string | null;
}

export interface CreateRouteDto {
  name?: string | null;
  source: string;
  destination: string;
  distance: number;
  estimatedMinutes?: number | null;
  basePrice: number;
  waypointsJson?: string | null;
  optimizeMode?: string | null;
}

export interface UpdateRouteDto extends CreateRouteDto {
  isActive: boolean;
}

export interface CreateRouteRequest {
  route: CreateRouteDto;
}

export interface UpdateRouteRequest {
  id: number;
  route: UpdateRouteDto;
}

export interface RouteFilter {
  search?: string;
  isActive?: boolean;
  distanceBand?: string;
  priceBand?: string;
}

export interface RouteListStats {
  total: number;
  short: number;
  medium: number;
  long: number;
}

export function parseRouteWaypoints(json?: string | null): string[] {
  if (!json) return [];
  try {
    const parsed = JSON.parse(json);
    return Array.isArray(parsed) ? parsed.filter((x): x is string => typeof x === 'string') : [];
  } catch {
    return [];
  }
}

export function serializeRouteWaypoints(stops: string[]): string | null {
  const cleaned = stops.map(s => s.trim()).filter(Boolean);
  return cleaned.length ? JSON.stringify(cleaned) : null;
}
