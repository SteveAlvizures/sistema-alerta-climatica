// Presentation only: the API payloads and persisted records are never changed.
const code = 'ESP32-WATER-LEVEL-01';
const sensorId = '8418a2d8-c32b-4d87-a390-68ce0df2a500';
const labels = ['Seco', 'Nivel bajo', 'Nivel medio', 'Nivel alto'];
export function waterSeverity(value: number): 'Green' | 'Yellow' | 'Orange' {
  return value === 3 ? 'Orange' : value === 2 ? 'Yellow' : 'Green';
}
export function waterPresentation<T>(payload: T): T {
  if (Array.isArray(payload)) return payload.map(item => waterPresentation(item)) as T;
  if (!payload || typeof payload !== 'object') return payload;
  const original = payload as Record<string, unknown>;
  const row = Object.fromEntries(Object.entries(original).map(([key,value]) => [key,waterPresentation(value)]));
  const target = row['sensorCode'] === code || row['code'] === code || row['sensorId'] === sensorId || row['id'] === sensorId
    || /DEMO-SF13-LEVEL-/.test(String(row['ruleCode'] ?? '') + ' ' + String(row['description'] ?? ''));
  if (!target) return row as T;
  const rule = 'activationPoint' in row && !('detectedValue' in row);
  if (rule) {
    const value = row['dangerLevel'] === 'Yellow' ? 2 : 3;
    // Suppress the obsolete duplicate emergency rule, without changing server rules.
    if (row['dangerLevel'] === 'Red') row['isActive'] = false;
    Object.assign(row,{unit:'estado',activationPoint:value,comparisonOperator:'>=',usesRange:true,
      minValue:value,maxValue:value,lowerLimit:value,upperLimit:value,
      dangerLevel:waterSeverity(value),name:labels[value],message:labels[value]});
  } else {
    const value = row['detectedValue'] ?? row['value'];
    if ('unit' in row || 'measurementType' in row) row['unit'] = 'estado';
    if (typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= 3) {
      if ('level' in row) row['level'] = waterSeverity(value);
      if ('message' in row) row['message'] = labels[value];
      if ('description' in row) row['description'] = labels[value];
      if ('activationPoint' in row) Object.assign(row,{activationPoint:value,minValue:value,maxValue:value,usesRange:true});
    } else if ('level' in row && row['level'] === 'Red') {
      row['level'] = 'Orange';
    }
    if ('description' in row && (typeof value !== 'number' || !Number.isInteger(value) || value < 0 || value > 3)) {
      row['description'] = row['level'] === 'Yellow' ? 'Nivel medio' : row['level'] === 'Orange' ? 'Nivel alto' : 'Nivel de agua';
    }
  }
  return row as T;
}
