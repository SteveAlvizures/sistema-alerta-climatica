import { EventStatisticsDto, RiskEventDetailDto, RiskEventDto } from '../../core/models/event.model';
import { PagedResponse } from '../../core/models/api.model';

export const riskEvent: RiskEventDto = {
  id: 'event-1', occurredAt: '2026-10-07T12:00:00Z', updatedAt: '2026-10-07T12:01:00Z', closedAt: null,
  communityId: 'community-1', communityName: 'La Isla', phenomenon: 'Flood', level: 'Orange', description: 'Incidente persistido',
  status: 'Open', sensorId: 'sensor-1', sensorName: 'Sensor de río', sensorCode: 'RIVER-01', value: 2.5, unit: 'm',
  representativeAlertId: 'alert-1', responsibleUserId: null, responsibleUserName: null, responsibility: null, alertCount: 1,
};
export const eventPage: PagedResponse<RiskEventDto> = { data: [riskEvent], pageIndex: 1, pageSize: 20, totalCount: 25, totalPages: 2, hasPrevious: false, hasNext: true };
export const eventStatistics: EventStatisticsDto = {
  total: 25, active: 20, closed: 5,
  byPhenomenon: [{ phenomenon: 'Flood', count: 10 }, { phenomenon: 'Drought', count: 5 }, { phenomenon: 'Storm', count: 4 }, { phenomenon: 'Frost', count: 3 }, { phenomenon: 'Wildfire', count: 3 }],
  byLevel: [{ level: 'Green', count: 0 }, { level: 'Yellow', count: 5 }, { level: 'Orange', count: 10 }, { level: 'Red', count: 10 }],
};
export const eventDetail: RiskEventDetailDto = {
  event: riskEvent,
  sensors: [{ id: 'sensor-1', name: 'Sensor de río', code: 'RIVER-01', communityId: 'community-1' }],
  alerts: [{ id: 'alert-1', communityId: 'community-1', ruleId: 'rule-1', supportingReadingId: 'reading-1', eventId: 'event-1', level: 'Orange', phenomenon: 'Flood', status: 'Acknowledged',
    message: 'Río elevado', detectedAt: riskEvent.occurredAt, updatedAt: riskEvent.updatedAt, closedAt: null,
    sensorId: 'sensor-1', sensorName: 'Sensor de río', sensorCode: 'RIVER-01', variable: 'RiverOrReservoirLevel', detectedValue: 2.5, activationPoint: 2, unit: 'm',
    ruleName: 'Regla de río', ruleCode: 'RIVER-RULE', usesRange: true, minValue: 2, maxValue: 3, acknowledgedAt: riskEvent.updatedAt, acknowledgedByName: 'Operadora', acknowledgedById: 'operator-1' }],
};
