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
  assetId?: string;
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

// Work Orders
export type WorkOrderStatus = "New" | "Assigned" | "InProgress" | "Completed";
export type WorkOrderPriority = "Low" | "Medium" | "High" | "Critical";

export interface AssignedTechnicianSummaryDto {
  id: string;
  displayName: string;
  specialty: string | null;
}

export interface AssignedTechnicianDetailDto {
  id: string;
  displayName: string;
  email: string;
  specialty: string | null;
  isActive: boolean;
}

export interface TechnicianNoteDto {
  id: string;
  technicianId: string;
  technicianDisplayName: string;
  content: string;
  createdAt: string;
}

export interface WorkOrderListItemDto {
  id: string;
  title: string;
  priority: WorkOrderPriority;
  status: WorkOrderStatus;
  securityAssetId: string;
  securityIncidentId: string | null;
  assetName: string | null;
  locationName: string | null;
  assignedTechnician: AssignedTechnicianSummaryDto | null;
  assignedAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface WorkOrderDetailDto {
  id: string;
  securityAssetId: string;
  securityIncidentId: string | null;
  title: string;
  description: string;
  priority: WorkOrderPriority;
  status: WorkOrderStatus;
  assetName: string | null;
  locationName: string | null;
  assignedTechnician: AssignedTechnicianDetailDto | null;
  assignedAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  completionSummary: string | null;
  createdAt: string;
  updatedAt: string;
  notes: TechnicianNoteDto[];
}

export interface CreateWorkOrderRequest {
  securityAssetId: string;
  securityIncidentId?: string;
  title: string;
  description: string;
  priority: WorkOrderPriority;
  assetName?: string;
  locationName?: string;
}

export interface AssignTechnicianRequest {
  technicianId: string;
}

export interface AddTechnicianNoteRequest {
  content: string;
}

export interface CompleteWorkOrderRequest {
  completionSummary?: string;
}

export interface WorkOrderSummaryDto {
  openCount: number;
  byStatus: {
    new: number;
    assigned: number;
    inProgress: number;
    completed: number;
  };
}

// Technicians
export interface TechnicianListItemDto {
  id: string;
  displayName: string;
  email: string;
  specialty: string | null;
  isActive: boolean;
}

export interface TechnicianDetailDto {
  id: string;
  displayName: string;
  email: string;
  specialty: string | null;
  isActive: boolean;
  createdAt: string;
}

// Credential Service
export type PersonType = "Employee" | "Contractor";
export type CredentialAccessLevel = "General" | "Clinical" | "Restricted" | "Security";
export type CredentialStatus = "Active" | "Expired" | "Revoked";

export interface PersonCredentialSummaryDto {
  activeCount: number;
  expiringSoonCount: number;
  revokedCount: number;
}

export interface PersonListItemDto {
  id: string;
  firstName: string;
  lastName: string;
  displayName: string;
  personType: PersonType;
  isActive: boolean;
  employeeNumber: string | null;
  email: string | null;
  department: string | null;
  jobTitle: string | null;
  credentialSummary: PersonCredentialSummaryDto;
}

export interface PersonCredentialDto {
  id: string;
  credentialNumber: string;
  accessLevel: CredentialAccessLevel;
  status: CredentialStatus;
  issuedAt: string;
  expiresAt: string;
  isExpiringSoon: boolean;
  revokedAt: string | null;
  revocationReason: string | null;
}

export interface PersonDetailDto {
  id: string;
  firstName: string;
  lastName: string;
  displayName: string;
  personType: PersonType;
  isActive: boolean;
  employeeNumber: string | null;
  email: string | null;
  department: string | null;
  jobTitle: string | null;
  createdAt: string;
  updatedAt: string | null;
  credentials: PersonCredentialDto[];
}

export interface CredentialPersonDto {
  id: string;
  displayName: string;
  personType: PersonType;
  isActive: boolean;
  employeeNumber: string | null;
  department: string | null;
  jobTitle: string | null;
}

export interface CredentialListItemDto {
  id: string;
  credentialNumber: string;
  accessLevel: CredentialAccessLevel;
  status: CredentialStatus;
  issuedAt: string;
  expiresAt: string;
  isExpiringSoon: boolean;
  revokedAt: string | null;
  person: CredentialPersonDto;
}

export interface CredentialDetailPersonDto {
  id: string;
  firstName: string;
  lastName: string;
  displayName: string;
  personType: PersonType;
  isActive: boolean;
  employeeNumber: string | null;
  email: string | null;
  department: string | null;
  jobTitle: string | null;
}

export interface CredentialDetailDto {
  id: string;
  credentialNumber: string;
  accessLevel: CredentialAccessLevel;
  status: CredentialStatus;
  issuedAt: string;
  expiresAt: string;
  isExpiringSoon: boolean;
  revokedAt: string | null;
  revocationReason: string | null;
  createdAt: string;
  updatedAt: string | null;
  person: CredentialDetailPersonDto;
}

export interface CredentialSummaryDto {
  activeCount: number;
  expiringSoonCount: number;
  expiredCount: number;
  revokedCount: number;
}

export interface IssueCredentialRequest {
  credentialNumber: string;
  accessLevel: CredentialAccessLevel;
  expiresAt: string;
}

export interface RevokeCredentialRequest {
  reason: string;
}
