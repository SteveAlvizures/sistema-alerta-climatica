export type ClimateVariable =
  | 'Temperature'
  | 'RelativeHumidity'
  | 'WindSpeed'
  | 'RainfallLevel'
  | 'RiverOrReservoirLevel';

export type ApiSensorOrigin = 'Simulated' | 'Physical';
export type ApiSensorStatus = 'Active' | 'Inactive';
export type ApiDangerLevel = 'Green' | 'Yellow' | 'Orange' | 'Red';
export type ApiAlertStatus = 'Open' | 'Acknowledged' | 'Closed';
export type ApiClimatePhenomenon = 'Flood' | 'Drought' | 'Storm' | 'Frost' | 'Wildfire';

export interface CommunityDto {
  id: string;
  name: string;
  location: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface SensorDto {
  id: string;
  communityId: string;
  code: string;
  name: string;
  measurementType: ClimateVariable;
  origin: ApiSensorOrigin;
  status: ApiSensorStatus;
  location: string;
  deviceCode: string | null;
  lastCommunicationAt: string | null;
  createdAt: string;
}

export interface SensorReadingDto {
  id: string;
  sensorId: string;
  variable: ClimateVariable;
  value: number;
  unit: string;
  measuredAt: string;
  receivedAt: string;
  origin: ApiSensorOrigin;
}

export interface AlertDto {
  id: string;
  communityId: string;
  ruleId: string;
  supportingReadingId: string;
  eventId: string | null;
  level: ApiDangerLevel;
  phenomenon: ApiClimatePhenomenon;
  status: ApiAlertStatus;
  message: string;
  detectedAt: string;
  updatedAt: string;
  closedAt: string | null;
}

export interface AlertRuleDto { id: string; communityId: string; sensorId: string | null; code: string; name: string; phenomenon: ApiClimatePhenomenon; variable: ClimateVariable; dangerLevel: ApiDangerLevel; lowerLimit: number | null; upperLimit: number | null; validFrom: string; validUntil: string | null; isActive: boolean; createdAt: string; }

export interface AuditActionDto {
  id: string;
  occurredAt: string;
  username: string;
  action: string;
  affectedEntity: string;
  affectedRecordId: string | null;
  description: string;
}
