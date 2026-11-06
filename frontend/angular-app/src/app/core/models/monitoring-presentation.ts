import { AlertDto, AlertRuleDto, ClimateVariable, SensorDto } from './api.model';
import { rainState, waterState, waterReading } from './physical-reading';

export const measurementNames: Record<ClimateVariable, string> = {
  Temperature: 'Temperatura', RelativeHumidity: 'Humedad relativa', RainfallLevel: 'Superficie de lluvia',
  RiverOrReservoirLevel: 'Nivel de agua', WindSpeed: 'Velocidad del viento', SmokeConcentration: 'Humo', OtherEnvironmental: 'Medición ambiental',
};
export const cleanNumber = (value: number): string => Number(value.toFixed(1)).toLocaleString('es-GT', { maximumFractionDigits: 1 });
export const unitLabel = (unit: string | null | undefined): string => unit === 'C' ? '°C' : unit ?? '';
export function sensorTitle(sensor: SensorDto): string { return sensor.origin === 'Physical' ? measurementNames[sensor.measurementType] : sensor.name; }
export function measurementValue(variable: string, value: number | null | undefined, unit: string | null | undefined, code?: string | null): string {
  if (value === null || value === undefined) return 'Sin lectura';
  if (variable === 'RainfallLevel' && (unit === 'estado' || code === 'ESP32-RAIN-01')) return rainState(value);
  if (variable === 'RiverOrReservoirLevel' && (unit === 'estado' || code === 'ESP32-WATER-LEVEL-01')) return waterReading(value, unit);
  return `${cleanNumber(value)} ${unitLabel(unit)}`.trim();
}
// Ranges are stored at decimal(18,6). Preserve the strict inequality while hiding the storage epsilon.
export function boundary(value: number, lower = true): string {
  const rounded = Math.round(value);
  if (Math.abs(value - rounded - (lower ? .000001 : -.000001)) < 1e-9) return `${lower ? '>' : '<'} ${cleanNumber(rounded)}`;
  return `${lower ? '≥' : '≤'} ${cleanNumber(value)}`;
}
export function thresholdLabel(rule: Pick<AlertRuleDto, 'activationPoint' | 'comparisonOperator' | 'unit' | 'variable'>): string {
  if (rule.variable === 'RainfallLevel' && rule.unit === 'estado') return rainState(rule.activationPoint);
  if (rule.variable === 'RiverOrReservoirLevel' && rule.unit === 'estado') return waterState(rule.activationPoint);
  const operator = rule.comparisonOperator;
  const text = operator === '>=' ? boundary(rule.activationPoint) : operator === '<=' ? boundary(rule.activationPoint, false) : `${operator} ${cleanNumber(rule.activationPoint)}`;
  return `${text} ${unitLabel(rule.unit)}`;
}
export function alertValue(alert: AlertDto): string { return measurementValue(alert.variable, alert.detectedValue, alert.unit, alert.sensorCode); }
export function alertTitle(alert: AlertDto, fallback: string): string {
  return alert.sensorCode?.startsWith('ESP32-') || alert.ruleCode?.startsWith('DEMO-SF13-') || alert.unit === 'estado'
    ? contextTitle(alert.variable, alert.level, alert.detectedValue) : fallback;
}
export function contextTitle(variable: string, level: string, value?: number | null): string {
  const critical = level === 'Red';
  return variable === 'Temperature' ? `Temperatura ${critical ? 'crítica' : 'elevada'}`
    : variable === 'RelativeHumidity' ? `Humedad ${critical ? 'crítica' : 'elevada'}`
    : variable === 'RiverOrReservoirLevel' ? `Nivel de agua ${critical ? 'crítico' : 'elevado'}`
    : variable === 'RainfallLevel' ? (value === 2 || level === 'Orange' ? 'Superficie muy mojada' : 'Superficie mojada')
    : 'Condición de monitoreo';
}
export function eventTitle(event: { description: string; level: string; sensorCode?: string | null; value?: number | null }, fallback: string): string {
  const hint = `${event.sensorCode ?? ''} ${event.description}`;
  const variable = /(?:DHT11-TEMP|SF13-TEMP)/.test(hint) ? 'Temperature' : /(?:DHT11-HUM|SF13-HUM)/.test(hint) ? 'RelativeHumidity'
    : /(?:WATER-LEVEL|SF13-LEVEL)/.test(hint) ? 'RiverOrReservoirLevel' : /(?:ESP32-RAIN|SF13-RAIN)/.test(hint) ? 'RainfallLevel' : '';
  return variable ? contextTitle(variable, event.level, event.value) : fallback;
}
