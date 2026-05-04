export interface PriceCalculationRequest {
  routeId: number;
  vehicleId: number;
  fuelPricePerLiter: number;
  driverAllowance: number;
  tollCharges: number;
  otherCharges: number;
  isRoundTrip?: boolean;
}

export interface PriceBreakdown {
  distance: number;
  fuelAverage: number;
  fuelPricePerLiter: number;
  fuelCost: number;
  driverAllowance: number;
  tollCharges: number;
  otherCharges: number;
  totalAmount: number;
  isRoundTrip?: boolean;
}
