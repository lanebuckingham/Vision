import type {
  PagedList,
  AssetListItemDto,
  AssetDetailDto,
  IncidentListItemDto,
  IncidentDetailDto,
  CreateIncidentRequest,
  UpdateIncidentStatusRequest,
  SecurityDashboardDto,
} from "./types";

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5100";

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
