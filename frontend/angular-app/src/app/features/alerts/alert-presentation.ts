import { measurementValue, boundary, thresholdLabel, alertValue, contextTitle, alertTitle } from '../../core/models/monitoring-presentation';
export { alertValue, contextTitle, alertTitle };
import { AlertRuleDto, AlertDto, ApiAlertStatus, ApiClimatePhenomenon, ApiDangerLevel, ClimateVariable } from '../../core/models/api.model';

export const alertStatusLabels: Record<ApiAlertStatus, string> = { Open: 'Activa', Acknowledged: 'Atendida', Closed: 'Cerrada' };
export const phenomenonLabels: Record<ApiClimatePhenomenon, string> = { Flood: 'Inundación', Drought: 'Sequía', Storm: 'Tormenta', Frost: 'Helada', Wildfire: 'Incendio forestal' };
export const levelLabels: Record<ApiDangerLevel, string> = { Green: 'Normal', Yellow: 'Precaución', Orange: 'Alerta', Red: 'Emergencia' };
export const variableLabels: Record<ClimateVariable, string> = { SmokeConcentration: 'Humo/incendio', OtherEnvironmental: 'Otro sensor ambiental', Temperature: 'Temperatura', RelativeHumidity: 'Humedad relativa', WindSpeed: 'Velocidad del viento', RainfallLevel: 'Nivel de lluvia', RiverOrReservoirLevel: 'Nivel de río o reservorio' };

export function conditionLabel(alert: AlertDto): string {
  if (alert.sensorCode === 'ESP32-WATER-LEVEL-01') return measurementValue(alert.variable,alert.detectedValue,'estado',alert.sensorCode);
  if (alert.usesRange) {
    const min = alert.minValue ?? null, max = alert.maxValue ?? null;
    if (alert.variable === 'RainfallLevel' && alert.unit === 'estado') return measurementValue(alert.variable,min ?? max,alert.unit);
    return `${min !== null ? boundary(min) : ''}${min !== null && max !== null ? ' y ' : ''}${max !== null ? boundary(max,false) : ''} ${alert.unit === 'C' ? '\u00b0C' : alert.unit}`.trim();
  }
  if (alert.usesRange == null) return `${alert.comparisonOperator ?? ''} ${alert.activationPoint} ${alert.unit} (umbral hist\u00f3rico)`.trim();
  return thresholdLabel({...alert,comparisonOperator:alert.comparisonOperator as AlertRuleDto['comparisonOperator'] ?? '>=',variable:alert.variable});
}
export function formatAlertDate(value: string | null | undefined): string {
  return value ? new Intl.DateTimeFormat('es-GT', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Guatemala' }).format(new Date(value)) : 'Sin registro histórico';
}
