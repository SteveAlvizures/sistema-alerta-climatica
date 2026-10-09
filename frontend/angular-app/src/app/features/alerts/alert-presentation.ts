import { AlertDto, ApiAlertStatus, ApiClimatePhenomenon, ApiDangerLevel, ClimateVariable } from '../../core/models/api.model';

export const alertStatusLabels: Record<ApiAlertStatus, string> = { Open: 'Activa', Acknowledged: 'Atendida', Closed: 'Cerrada' };
export const phenomenonLabels: Record<ApiClimatePhenomenon, string> = { Flood: 'Inundación', Drought: 'Sequía', Storm: 'Tormenta', Frost: 'Helada', Wildfire: 'Incendio forestal' };
export const levelLabels: Record<ApiDangerLevel, string> = { Green: 'Normal', Yellow: 'Precaución', Orange: 'Alerta', Red: 'Emergencia' };
export const variableLabels: Record<ClimateVariable, string> = { SmokeConcentration: 'Humo/incendio', OtherEnvironmental: 'Otro sensor ambiental', Temperature: 'Temperatura', RelativeHumidity: 'Humedad relativa', WindSpeed: 'Velocidad del viento', RainfallLevel: 'Nivel de lluvia', RiverOrReservoirLevel: 'Nivel de río o reservorio' };

export function conditionLabel(alert: AlertDto): string {
  if (alert.usesRange) {
    const min = alert.minValue ?? null; const max = alert.maxValue ?? null;
    return `${min !== null && max !== null ? `${min} a ${max}` : min !== null ? `${min} o más` : `${max} o menos`} ${alert.unit}`;
  }
  return `${alert.comparisonOperator ?? ''} ${alert.activationPoint} ${alert.unit}${alert.usesRange == null ? ' (umbral histórico)' : ''}`.trim();
}
export function formatAlertDate(value: string | null | undefined): string {
  return value ? new Intl.DateTimeFormat('es-GT', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Guatemala' }).format(new Date(value)) : 'Sin registro histórico';
}
