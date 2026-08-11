"use client";

import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import { getIncidents } from "@/lib/api/client";
import type { PagedList, IncidentListItemDto, IncidentSeverity, IncidentStatus } from "@/lib/api/types";

const STATUS_OPTIONS: IncidentStatus[] = ["Open", "Investigating", "Resolved"];
const SEVERITY_OPTIONS: IncidentSeverity[] = ["Critical", "High", "Medium", "Low"];

export default function IncidentsPage() {
  const [data, setData] = useState<PagedList<IncidentListItemDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [severityFilter, setSeverityFilter] = useState("");
  const [page, setPage] = useState(1);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await getIncidents({
        search: search || undefined,
        status: statusFilter || undefined,
        severity: severityFilter || undefined,
        page,
        pageSize: 20,
      });
      setData(result);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load incidents");
    } finally {
      setLoading(false);
    }
  }, [search, statusFilter, severityFilter, page]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const totalPages = data ? Math.ceil(data.totalCount / data.pageSize) : 0;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Security Incidents</h1>
        <Link
          href="/incidents/new"
          className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          Create Incident
        </Link>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <input
          type="search"
          placeholder="Search incidents..."
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1); }}
          className="rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
          aria-label="Search incidents"
        />
        <select
          value={statusFilter}
          onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
          className="rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
          aria-label="Filter by status"
        >
          <option value="">All Statuses</option>
          {STATUS_OPTIONS.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <select
          value={severityFilter}
          onChange={(e) => { setSeverityFilter(e.target.value); setPage(1); }}
          className="rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
          aria-label="Filter by severity"
        >
          <option value="">All Severities</option>
          {SEVERITY_OPTIONS.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
      </div>

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 dark:border-red-900 dark:bg-red-950">
          <p className="text-sm text-red-700 dark:text-red-300">{error}</p>
        </div>
      )}

      {loading && !data && (
        <div className="space-y-2 animate-pulse">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="h-20 rounded-lg bg-gray-100 dark:bg-gray-800" />
          ))}
        </div>
      )}

      {data && (
        <>
          {data.items.length === 0 ? (
            <p className="py-8 text-center text-sm text-gray-500 dark:text-gray-400">
              No incidents found matching your filters.
            </p>
          ) : (
            <div className="space-y-2">
              {data.items.map((incident) => (
                <Link
                  key={incident.id}
                  href={`/incidents/${incident.id}`}
                  className="flex items-start gap-4 rounded-lg border border-gray-200 p-4 transition-colors hover:bg-gray-50 dark:border-gray-800 dark:hover:bg-gray-900"
                >
                  <SeverityIndicator severity={incident.severity} />
                  <div className="flex-1 min-w-0">
                    <p className="font-medium text-gray-900 dark:text-white">{incident.title}</p>
                    <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
                      {incident.asset ? `${incident.asset.name} · ` : ""}{incident.location.name}
                    </p>
                  </div>
                  <div className="flex flex-col items-end gap-1">
                    <StatusBadge status={incident.status} />
                    <time className="text-xs text-gray-500 dark:text-gray-500">
                      {new Date(incident.createdAt).toLocaleDateString()}
                    </time>
                  </div>
                </Link>
              ))}
            </div>
          )}

          {totalPages > 1 && (
            <div className="flex items-center justify-between">
              <p className="text-sm text-gray-500 dark:text-gray-400">
                {data.totalCount} incidents total
              </p>
              <div className="flex gap-2">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page <= 1}
                  className="rounded border border-gray-300 px-3 py-1 text-sm disabled:opacity-50 dark:border-gray-700"
                >
                  Previous
                </button>
                <span className="px-3 py-1 text-sm text-gray-600 dark:text-gray-400">
                  Page {page} of {totalPages}
                </span>
                <button
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  disabled={page >= totalPages}
                  className="rounded border border-gray-300 px-3 py-1 text-sm disabled:opacity-50 dark:border-gray-700"
                >
                  Next
                </button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}

function SeverityIndicator({ severity }: { severity: IncidentSeverity }) {
  const colors = {
    Critical: "bg-red-500",
    High: "bg-orange-500",
    Medium: "bg-yellow-500",
    Low: "bg-gray-400",
  }[severity];

  return (
    <div className="flex flex-col items-center gap-1 pt-1">
      <span className={`h-3 w-3 rounded-full ${colors}`} aria-label={`${severity} severity`} />
      <span className="text-[10px] text-gray-500 dark:text-gray-500">{severity}</span>
    </div>
  );
}

function StatusBadge({ status }: { status: IncidentStatus }) {
  const styles = {
    Open: "bg-blue-100 text-blue-700 dark:bg-blue-900 dark:text-blue-300",
    Investigating: "bg-yellow-100 text-yellow-700 dark:bg-yellow-900 dark:text-yellow-300",
    Resolved: "bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300",
  }[status];

  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${styles}`}>
      {status}
    </span>
  );
}
