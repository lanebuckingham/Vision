import type {
  PagedList,
  AssetListItemDto,
  AssetDetailDto,
  IncidentListItemDto,
  IncidentDetailDto,
  CreateIncidentRequest,
  UpdateIncidentStatusRequest,
  SecurityDashboardDto,
  WorkOrderListItemDto,
  WorkOrderDetailDto,
  CreateWorkOrderRequest,
  AssignTechnicianRequest,
  AddTechnicianNoteRequest,
  CompleteWorkOrderRequest,
  WorkOrderSummaryDto,
  TechnicianListItemDto,
  TechnicianDetailDto,
  TechnicianNoteDto,
} from "./types";

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5163";
const WO_API_BASE = process.env.NEXT_PUBLIC_WORK_ORDER_API_URL || "http://localhost:5250";

async function fetchApi<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options?.headers,
    },
  });

  if (!res.ok) {
    const body = await res.json().catch(() => null);
    const message = body?.detail || body?.title || `API error: ${res.status}`;
    throw new ApiError(res.status, message, body);
  }

  return res.json();
}

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public body: unknown
  ) {
    super(message);
    this.name = "ApiError";
  }
}

// Assets
export function getAssets(params?: {
  status?: string;
  type?: string;
  buildingId?: string;
  locationId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedList<AssetListItemDto>> {
  const searchParams = new URLSearchParams();
  if (params?.status) searchParams.set("status", params.status);
  if (params?.type) searchParams.set("type", params.type);
  if (params?.buildingId) searchParams.set("buildingId", params.buildingId);
  if (params?.locationId) searchParams.set("locationId", params.locationId);
  if (params?.search) searchParams.set("search", params.search);
  if (params?.page) searchParams.set("page", params.page.toString());
  if (params?.pageSize) searchParams.set("pageSize", params.pageSize.toString());

  const qs = searchParams.toString();
  return fetchApi(`/api/v1/assets${qs ? `?${qs}` : ""}`);
}

export function getAssetById(id: string): Promise<AssetDetailDto> {
  return fetchApi(`/api/v1/assets/${id}`);
}

export function updateAssetStatus(id: string, status: string): Promise<AssetDetailDto> {
  return fetchApi(`/api/v1/assets/${id}/status`, {
    method: "PATCH",
    body: JSON.stringify({ status }),
  });
}

// Incidents
export function getIncidents(params?: {
  status?: string;
  severity?: string;
  assetId?: string;
  buildingId?: string;
  locationId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedList<IncidentListItemDto>> {
  const searchParams = new URLSearchParams();
  if (params?.status) searchParams.set("status", params.status);
  if (params?.severity) searchParams.set("severity", params.severity);
  if (params?.assetId) searchParams.set("assetId", params.assetId);
  if (params?.buildingId) searchParams.set("buildingId", params.buildingId);
  if (params?.locationId) searchParams.set("locationId", params.locationId);
  if (params?.search) searchParams.set("search", params.search);
  if (params?.page) searchParams.set("page", params.page.toString());
  if (params?.pageSize) searchParams.set("pageSize", params.pageSize.toString());

  const qs = searchParams.toString();
  return fetchApi(`/api/v1/incidents${qs ? `?${qs}` : ""}`);
}

export function getIncidentById(id: string): Promise<IncidentDetailDto> {
  return fetchApi(`/api/v1/incidents/${id}`);
}

export function createIncident(data: CreateIncidentRequest): Promise<IncidentDetailDto> {
  return fetchApi("/api/v1/incidents", {
    method: "POST",
    body: JSON.stringify(data),
  });
}

export function updateIncidentStatus(
  id: string,
  data: UpdateIncidentStatusRequest
): Promise<IncidentDetailDto> {
  return fetchApi(`/api/v1/incidents/${id}`, {
    method: "PATCH",
    body: JSON.stringify(data),
  });
}

// Dashboard
export function getDashboard(): Promise<SecurityDashboardDto> {
  return fetchApi("/api/v1/dashboard");
}

// Work Order Service helper
async function fetchWoApi<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${WO_API_BASE}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options?.headers,
    },
  });

  if (!res.ok) {
    const body = await res.json().catch(() => null);
    const message = body?.detail || body?.title || `API error: ${res.status}`;
    throw new ApiError(res.status, message, body);
  }

  return res.json();
}

// Work Orders
export function getWorkOrders(params?: {
  status?: string;
  priority?: string;
  technicianId?: string;
  assetId?: string;
  incidentId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedList<WorkOrderListItemDto>> {
  const searchParams = new URLSearchParams();
  if (params?.status) searchParams.set("status", params.status);
  if (params?.priority) searchParams.set("priority", params.priority);
  if (params?.technicianId) searchParams.set("technicianId", params.technicianId);
  if (params?.assetId) searchParams.set("assetId", params.assetId);
  if (params?.incidentId) searchParams.set("incidentId", params.incidentId);
  if (params?.search) searchParams.set("search", params.search);
  if (params?.page) searchParams.set("page", params.page.toString());
  if (params?.pageSize) searchParams.set("pageSize", params.pageSize.toString());

  const qs = searchParams.toString();
  return fetchWoApi(`/api/v1/work-orders${qs ? `?${qs}` : ""}`);
}

export function getWorkOrderById(id: string): Promise<WorkOrderDetailDto> {
  return fetchWoApi(`/api/v1/work-orders/${id}`);
}

export function createWorkOrder(data: CreateWorkOrderRequest): Promise<WorkOrderDetailDto> {
  return fetchWoApi("/api/v1/work-orders", {
    method: "POST",
    body: JSON.stringify(data),
  });
}

export function assignTechnician(workOrderId: string, data: AssignTechnicianRequest): Promise<WorkOrderDetailDto> {
  return fetchWoApi(`/api/v1/work-orders/${workOrderId}/assignment`, {
    method: "POST",
    body: JSON.stringify(data),
  });
}

export function startWork(workOrderId: string): Promise<WorkOrderDetailDto> {
  return fetchWoApi(`/api/v1/work-orders/${workOrderId}/start`, {
    method: "POST",
  });
}

export function addTechnicianNote(workOrderId: string, data: AddTechnicianNoteRequest): Promise<TechnicianNoteDto> {
  return fetchWoApi(`/api/v1/work-orders/${workOrderId}/notes`, {
    method: "POST",
    body: JSON.stringify(data),
  });
}

export function completeWorkOrder(workOrderId: string, data: CompleteWorkOrderRequest): Promise<WorkOrderDetailDto> {
  return fetchWoApi(`/api/v1/work-orders/${workOrderId}/complete`, {
    method: "POST",
    body: JSON.stringify(data),
  });
}

export function getWorkOrderSummary(): Promise<WorkOrderSummaryDto> {
  return fetchWoApi("/api/v1/work-orders/summary");
}

// Technicians
export function getTechnicians(params?: {
  activeOnly?: boolean;
  search?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedList<TechnicianListItemDto>> {
  const searchParams = new URLSearchParams();
  if (params?.activeOnly !== undefined) searchParams.set("activeOnly", params.activeOnly.toString());
  if (params?.search) searchParams.set("search", params.search);
  if (params?.page) searchParams.set("page", params.page.toString());
  if (params?.pageSize) searchParams.set("pageSize", params.pageSize.toString());

  const qs = searchParams.toString();
  return fetchWoApi(`/api/v1/technicians${qs ? `?${qs}` : ""}`);
}

export function getTechnicianById(id: string): Promise<TechnicianDetailDto> {
  return fetchWoApi(`/api/v1/technicians/${id}`);
}
