export interface TrackerBrand {
  id: number;
  name: string;
  logoUrl?: string;
  isActive: boolean;
}

export interface TrackerModel {
  id: number;
  trackerBrandId: number;
  brandName: string;
  name: string;
  protocol: string;
  protocolLabel: string;
  defaultPort: number;
  supportsEngineCutOff: boolean;
  supportsFuelSensor: boolean;
  supportsTemperatureSensor: boolean;
  supportsDriverIdentification: boolean;
  supportsCanBus: boolean;
  supportsObd: boolean;
  supportsBle: boolean;
  supportsCamera: boolean;
  supportsRelay: boolean;
  supportsDoorSensor: boolean;
  supportsIgnition: boolean;
  supportsOdometer: boolean;
  supportsBatteryMonitoring: boolean;
  defaultRelayOutput?: string;
  catalogKey?: string;
  description?: string;
  isActive: boolean;
}
