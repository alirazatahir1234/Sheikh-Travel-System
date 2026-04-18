export interface Route {
  id: number;
  name: string;
  origin: string;
  destination: string;
  distanceKm: number;
  estimatedMinutes: number;
  isActive: boolean;
}

export interface CreateRouteRequest {
  name: string;
  origin: string;
  destination: string;
  distanceKm: number;
  estimatedMinutes: number;
}

export interface UpdateRouteRequest extends CreateRouteRequest {
  id: number;
}
