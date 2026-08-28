export interface ClimateIndicator {
  key: 'temperature' | 'humidity' | 'wind' | 'rain' | 'river';
  name: string;
  value: string;
  detail: string;
  sensorId?: string;
  status?: 'Normal' | 'Preventiva' | 'Alta' | 'Crítica';
  tone?: 'green' | 'yellow' | 'orange' | 'red';
  lastReading?: string;
  nextActivation?: string;
  trend?: Array<{ label: string; value: number; unit: string }>;
}
