export type ClimateVariable =
  | 'Temperature'
  | 'RelativeHumidity'
  | 'WindSpeed'
  | 'RainfallLevel'
  | 'RiverOrReservoirLevel';

export type ApiSensorOrigin = 'Simulated' | 'Physical';
export type ApiSensorStatus = 'Active' | 'Inactive';

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
