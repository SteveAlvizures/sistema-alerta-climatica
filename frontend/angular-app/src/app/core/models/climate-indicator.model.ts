export interface ClimateIndicator {
  key: 'temperature' | 'humidity' | 'wind' | 'rain' | 'river';
  name: string;
  value: string;
  detail: string;
}
