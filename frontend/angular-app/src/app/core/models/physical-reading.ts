import { SensorDto, SensorReadingDto } from './api.model';

export function isEsp32Rain(sensor: SensorDto): boolean {
  return sensor.origin === 'Physical' && sensor.measurementType === 'RainfallLevel'
    && (sensor.unit === 'estado' || sensor.code === 'ESP32-RAIN-01');
}

export function rainState(value: number): string {
  return ({ 0: 'Seco', 1: 'Mojado leve', 2: 'Muy mojado' } as Record<number, string>)[value] ?? 'Estado desconocido';
}

export function formatSensorReading(sensor: SensorDto, reading: SensorReadingDto): string {
  if (isEsp32Rain(sensor)) {
    return rainState(reading.value);
  }
  if (sensor.origin === 'Physical' && sensor.code.startsWith('ESP32-')) {
    if (sensor.measurementType === 'Temperature' && ['C', '°C'].includes(reading.unit)) return `${reading.value.toFixed(1)} °C`;
    if (sensor.measurementType === 'RelativeHumidity') return `${reading.value.toFixed(1)} %`;
    if (sensor.measurementType === 'RiverOrReservoirLevel' && reading.unit === '%') return `${Math.round(reading.value)} %`;
  }
  return `${reading.value} ${reading.unit}`;
}
