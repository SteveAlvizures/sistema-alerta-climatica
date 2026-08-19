export type SensorStatus = 'Activo' | 'Inactivo';
export type SensorOrigin = 'Simulated' | 'Physical';

export interface ClimateSensor {
  name: string;
  measurementType: string;
  status: SensorStatus;
  origin: SensorOrigin;
  lastCommunication: string;
}

export const sensorOriginLabels: Record<SensorOrigin, string> = {
  Simulated: 'Simulado',
  Physical: 'Físico',
};
