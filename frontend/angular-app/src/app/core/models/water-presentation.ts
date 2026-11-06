// Display units only. Severity, rules and alert/event identity come from the real API.
const code = 'ESP32-WATER-LEVEL-01';
const sensorId = '8418a2d8-c32b-4d87-a390-68ce0df2a500';
export function waterPresentation<T>(payload: T): T {
  if (Array.isArray(payload)) return payload.map(item => waterPresentation(item)) as T;
  if (!payload || typeof payload !== 'object') return payload;
  const row = Object.fromEntries(Object.entries(payload).map(([key,value]) => [key,waterPresentation(value)]));
  const target = row['sensorCode'] === code || row['code'] === code || row['sensorId'] === sensorId || row['id'] === sensorId;
  const rule = 'activationPoint' in row && !('detectedValue' in row);
  if (target && !rule && ('unit' in row || 'measurementType' in row)) row['unit'] = 'estado';
  return row as T;
}
