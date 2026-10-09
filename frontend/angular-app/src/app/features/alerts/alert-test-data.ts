import { AlertDto } from '../../core/models/api.model';

export const alertTestData: AlertDto = {
  id: 'alert-1', communityId: 'community-1', communityName: 'El Pinar', ruleId: 'rule-1',
  ruleName: 'Regla de inundación', ruleCode: 'FLOOD-01', supportingReadingId: 'reading-1', eventId: 'event-1',
  sensorId: 'sensor-1', sensorName: 'Pluviómetro central', sensorCode: 'RAIN-01', variable: 'RainfallLevel',
  level: 'Yellow', phenomenon: 'Flood', status: 'Open', message: 'Lluvia en rango de riesgo.',
  detectedAt: '2026-10-06T12:00:00Z', updatedAt: '2026-10-06T12:00:00Z', closedAt: null,
  detectedValue: 15, activationPoint: 10, minValue: 10, maxValue: 20, usesRange: true, comparisonOperator: '>=', unit: 'mm',
  acknowledgedAt: null, acknowledgedById: null, acknowledgedByName: null, closedById: null, closedByName: null,
};
