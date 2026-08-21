export type DangerLevel = 'Verde' | 'Amarillo' | 'Naranja' | 'Rojo';

export interface ClimateAlert {
  id?: string;
  level: DangerLevel;
  phenomenon: string;
  message: string;
  occurredAt: string;
  status: string;
  apiStatus?: 'Open' | 'Acknowledged' | 'Closed';
  tone: 'green' | 'yellow' | 'orange' | 'red';
  hasEvent: boolean;
}

export interface RecentClimateEvent {
  title: string;
  detail: string;
  occurredAt: string;
  tone: 'green' | 'yellow' | 'orange' | 'red' | 'neutral';
}
