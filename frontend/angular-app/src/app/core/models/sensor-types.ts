import { SensorType } from './api.model';

export const sensorTypes: ReadonlyArray<{ value: SensorType; label: string; variable: string; unit: string }> = [
  { value: 'Temperature', label: 'Temperatura', variable: 'Temperature', unit: '°C' },
  { value: 'Humidity', label: 'Humedad', variable: 'RelativeHumidity', unit: '%' },
  { value: 'WindSpeed', label: 'Velocidad del viento', variable: 'WindSpeed', unit: 'km/h' },
  { value: 'Rainfall', label: 'Lluvia', variable: 'RainfallLevel', unit: 'mm' },
  { value: 'RiverLevel', label: 'Nivel de río', variable: 'RiverOrReservoirLevel', unit: 'm' },
  { value: 'ReservoirLevel', label: 'Nivel de reservorio', variable: 'RiverOrReservoirLevel', unit: 'm' },
  { value: 'SmokeFire', label: 'Humo/incendio', variable: 'SmokeConcentration', unit: 'ppm' },
  { value: 'OtherEnvironmental', label: 'Otro sensor ambiental', variable: 'OtherEnvironmental', unit: 'u' },
];
