enum TenantType {
  travelAgency,
  fleetOperator,
  corporateCustomer,
  driver,
}

extension TenantTypeX on TenantType {
  String get label => switch (this) {
        TenantType.travelAgency => 'Travel Agency',
        TenantType.fleetOperator => 'Fleet Operator',
        TenantType.corporateCustomer => 'Corporate Client',
        TenantType.driver => 'Driver',
      };

  String get productSubtitle => switch (this) {
        TenantType.travelAgency => 'CONTROL CENTER',
        TenantType.fleetOperator => 'FLEET CONTROL',
        TenantType.corporateCustomer => 'CORPORATE PORTAL',
        TenantType.driver => 'DRIVER APP',
      };

  String get accountTierLabel => switch (this) {
        TenantType.travelAgency => 'Premium Account',
        TenantType.fleetOperator => 'Fleet Enterprise',
        TenantType.corporateCustomer => 'Corporate Account',
        TenantType.driver => 'Driver Account',
      };
}

TenantType resolveTenantType({
  required List<String> roles,
  TenantType? override,
}) {
  if (override != null) return override;
  if (roles.contains('Driver')) return TenantType.driver;
  if (roles.contains('FleetManager')) return TenantType.fleetOperator;
  if (roles.contains('CorporateUser')) return TenantType.corporateCustomer;
  return TenantType.travelAgency;
}
