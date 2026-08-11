// API response types matching SecurityOperationsService DTOs

// Common
export interface PagedList<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

// Shared nested
export interface BuildingDto {
  id: string;
  name: string;
}

export interface LocationDto {
  id: string;
  name: string;
  floor: string | null;
  department: string | null;
}

// Assets
export interface AssetListItemDto {
  id: string;
  name: string;
  assetTag: string | null;
  assetType: AssetType;
  status: AssetStatus;
  building: BuildingDto;
  location: LocationDto;
  lastServiceAt: string | null;
  statusChangedAt: string | null;
}

export interface AssetIncidentDto {
  id: string;
  title: string;
  severity: IncidentSeverity;
  status: IncidentStatus;
  createdAt: string;
  workOrderId: string | null;
}

export interface AssetDetailDto {
  id: string;
  name: string;
  assetTag: string | null;
  assetType: AssetType;
  status: AssetStatus;
  manufacturer: string | null;
  model: string | null;
  description: string | null;
  building: BuildingDto;
  location: LocationDto;
  lastServiceAt: string | null;
  statusChangedAt: string | null;
  recentIncidents: AssetIncidentDto[];
}

// Incidents
export interface IncidentAssetDto {
  id: string;
  name: string;
  assetType: AssetType;
}

export interface IncidentAssetDetailDto {
  id: string;
  name: string;
  assetTag: string | null;
  assetType: AssetType;
  status: AssetStatus;
}

export interface IncidentListItemDto {
  id: string;
  title: string;
  severity: IncidentSeverity;
  status: IncidentStatus;
  asset: IncidentAssetDto | null;
  location: LocationDto;
  createdAt: string;
  resolvedAt: string | null;
  workOrderId: string | null;
}

export interface IncidentDetailDto {
  id: string;
  title: string;
  description: string;
  severity: IncidentSeverity;
  status: IncidentStatus;
  resolutionSummary: string | null;
  createdAt: string;
  updatedAt: string;
  resolvedAt: string | null;
  workOrderId: string | null;
  asset: IncidentAssetDetailDto | null;
  location: LocationDto;
  building: BuildingDto;
}

export interface CreateIncidentRequest {
  locationId: string;
  securityAssetId?: string;
  title: string;
  description: string;
  severity: IncidentSeverity;
}

export interface UpdateIncidentStatusRequest {
  status: IncidentStatus;
  resolutionSummary?: string;
}

// Dashboard
export interface DashboardHospitalDto {
  id: string;
  name: string;
}

export interface SecurityHealthDto {
  operationalPercentage: number;
  operationalAssets: number;
  totalAssets: number;
  degradedAssets: number;
  offlineAssets: number;
}

export interface DashboardIncidentsDto {
  activeCritical: number;
  activeTotal: number;
}

export interface CriticalAlertDto {
  incidentId: string;
  title: string;
  severity: IncidentSeverity;
  status: IncidentStatus;
  assetId: string | null;
  assetName: string | null;
  assetType: string | null;
  locationName: string;
  createdAt: string;
}

export interface RecentActivityDto {
  type: string;
  title: string;
  occurredAt: string;
  incidentId: string | null;
  assetId: string | null;
}

export interface SecurityDashboardDto {
  hospital: DashboardHospitalDto;
  securityHealth: SecurityHealthDto;
  incidents: DashboardIncidentsDto;
  criticalAlerts: CriticalAlertDto[];
  recentActivity: RecentActivityDto[];
}

// Enums as string unions
export type AssetType = "Camera" | "AccessControlledDoor" | "BadgeReader" | "SecurityGate";
export type AssetStatus = "Operational" | "Degraded" | "Offline";
export type IncidentSeverity = "Low" | "Medium" | "High" | "Critical";
export type IncidentStatus = "Open" | "Investigating" | "Resolved";
