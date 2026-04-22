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
}

export interface CreateRouteDto {
  name?: string | null;
  source: string;
  destination: string;
  distance: number;
  estimatedMinutes?: number | null;
  basePrice: number;
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
