export type ReportKind = 'communities' | 'sensors' | 'readings' | 'alerts' | 'events' | 'sensor-status' | 'statistics' | 'executive';
export type ReportState = 'Pending' | 'Processing' | 'Completed' | 'Failed';
export interface ReportType { id: ReportKind; name: string; states: string[]; supportsDates: boolean; supportsSensor: boolean; supportsVariable: boolean; }
export interface ReportFilters { communityId?: string; sensorId?: string; dateFrom?: string; dateTo?: string; variable?: string; status?: string; }
export interface ReportRequest { type: ReportKind; filters: ReportFilters; }
export interface ReportMetadata { id: string; type: ReportKind; user: { id: string; name: string }; requestedAt: string; filters: ReportFilters; state: ReportState; filename: string | null; generatedAt: string | null; downloadedAt: string | null; size: number | null; error: string | null; correlationId: string; }
export interface ReportHistoryFilters { page: number; pageSize: number; type?: string; state?: string; }
