export interface PriceCalculationRequest {
  routeId: number;
  vehicleId: number;
  passengerCount: number;
  fuelPricePerLiter?: number;
  driverAllowance?: number;
  tollCharges?: number;
  otherCharges?: number;
}

export interface PriceBreakdown {
  distanceKm: number;
  fuelAverage: number;
  fuelPricePerLiter: number;
  fuelCost: number;
  driverAllowance: number;
  tollCharges: number;
  otherCharges: number;
  totalAmount: number;
}
