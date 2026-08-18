"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { getWorkOrders } from "@/lib/api/client";
import type { PagedList, WorkOrderListItemDto, WorkOrderStatus, WorkOrderPriority } from "@/lib/api/types";

const STATUS_OPTIONS: WorkOrderStatus[] = ["New", "Assigned", "InProgress", "Completed"];
const PRIORITY_OPTIONS: WorkOrderPriority[] = ["Critical", "High", "Medium", "Low"];

export default function WorkOrderListPage() {
  const [data, setData] = useState<PagedList<WorkOrderListItemDto> | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const [statusFilter, setStatusFilter] = useState("");
  const [priorityFilter, setPriorityFilter] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

  const beginQueryRefresh = () => {
    setLoading(true);
    setError(null);
  };

  useEffect(() => {
    let cancelled = false;
    getWorkOrders({
      status: statusFilter || undefined,
      priority: priorityFilter || undefined,
      search: search || undefined,
      page,
      pageSize: 25,
    })
      .then((result) => {
        if (cancelled) return;
        setData(result);
        setError(null);
      })
      .catch((e) => {
        if (cancelled) return;
        setError(e instanceof Error ? e.message : "Failed to load work orders");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [statusFilter, priorityFilter, search, page]);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Work Orders</h1>
          <p className="text-sm text-gray-500 dark:text-gray-400">Maintenance and repair management</p>
        </div>
        <Link
          href="/work-orders/new"
          className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          Create Work Order
        </Link>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <select
          value={statusFilter}
          onChange={(e) => { beginQueryRefresh(); setStatusFilter(e.target.value); setPage(1); }}
          aria-label="Filter by status"
          className="rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
        >
          <option value="">All statuses</option>
          {STATUS_OPTIONS.map((s) => (
            <option key={s} value={s}>{s === "InProgress" ? "In Progress" : s}</option>
          ))}
        </select>
        <select
          value={priorityFilter}
          onChange={(e) => { beginQueryRefresh(); setPriorityFilter(e.target.value); setPage(1); }}
          aria-label="Filter by priority"
          className="rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
        >
          <option value="">All priorities</option>
          {PRIORITY_OPTIONS.map((p) => (
            <option key={p} value={p}>{p}</option>
          ))}
        </select>
        <input
          type="text"
          placeholder="Search work orders..."
          value={search}
          onChange={(e) => { beginQueryRefresh(); setSearch(e.target.value); setPage(1); }}
          className="rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
        />
      </div>

      {/* Content */}
      {loading && <WorkOrderListSkeleton />}

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-6 dark:border-red-900 dark:bg-red-950">
          <h2 className="text-lg font-semibold text-red-800 dark:text-red-200">Unable to load work orders</h2>
          <p className="mt-2 text-sm text-red-600 dark:text-red-400">{error}</p>
        </div>
      )}

      {!loading && !error && data && data.items.length === 0 && (
        <div className="rounded-lg border border-gray-200 p-8 text-center dark:border-gray-800">
          <p className="text-sm text-gray-500 dark:text-gray-400">No work orders found.</p>
        </div>
      )}

      {!loading && !error && data && data.items.length > 0 && (
        <>
          <div className="space-y-2">
            {data.items.map((wo) => (
              <Link
                key={wo.id}
                href={`/work-orders/${wo.id}`}
                className="flex items-center gap-4 rounded-lg border border-gray-200 p-4 transition-colors hover:bg-gray-50 dark:border-gray-800 dark:hover:bg-gray-800"
              >
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <PriorityBadge priority={wo.priority} />
                    <StatusBadge status={wo.status} />
                  </div>
                  <p className="mt-1 font-medium text-gray-900 dark:text-white truncate">{wo.title}</p>
                  <p className="text-sm text-gray-500 dark:text-gray-400">
                    {wo.assetName && `${wo.assetName} · `}{wo.locationName || "No location"}
                  </p>
                </div>
                <div className="hidden sm:block text-right">
                  {wo.assignedTechnician && (
                    <p className="text-sm text-gray-700 dark:text-gray-300">{wo.assignedTechnician.displayName}</p>
                  )}
                  <p className="text-xs text-gray-500 dark:text-gray-500">
                    {new Date(wo.createdAt).toLocaleDateString()}
                  </p>
                </div>
              </Link>
            ))}
          </div>

          {/* Pagination */}
          {data.totalCount > data.pageSize && (
            <div className="flex items-center justify-between">
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Page {data.page} of {Math.ceil(data.totalCount / data.pageSize)} ({data.totalCount} total)
              </p>
              <div className="flex gap-2">
                <button
                  onClick={() => { beginQueryRefresh(); setPage((p) => Math.max(1, p - 1)); }}
                  disabled={page === 1}
                  className="rounded-lg border border-gray-300 px-3 py-1 text-sm disabled:opacity-50 dark:border-gray-700 dark:text-gray-300"
                >
                  Previous
                </button>
                <button
                  onClick={() => { beginQueryRefresh(); setPage((p) => p + 1); }}
                  disabled={page >= Math.ceil(data.totalCount / data.pageSize)}
                  className="rounded-lg border border-gray-300 px-3 py-1 text-sm disabled:opacity-50 dark:border-gray-700 dark:text-gray-300"
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

function PriorityBadge({ priority }: { priority: WorkOrderPriority }) {
  const styles: Record<WorkOrderPriority, string> = {
    Critical: "bg-red-100 text-red-700 dark:bg-red-900 dark:text-red-300",
    High: "bg-orange-100 text-orange-700 dark:bg-orange-900 dark:text-orange-300",
    Medium: "bg-yellow-100 text-yellow-700 dark:bg-yellow-900 dark:text-yellow-300",
    Low: "bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300",
  };

  return (
    <span className={`text-xs font-medium rounded px-2 py-0.5 ${styles[priority]}`}>
      {priority}
    </span>
  );
}

function StatusBadge({ status }: { status: WorkOrderStatus }) {
  const styles: Record<WorkOrderStatus, string> = {
    New: "bg-blue-100 text-blue-700 dark:bg-blue-900 dark:text-blue-300",
    Assigned: "bg-purple-100 text-purple-700 dark:bg-purple-900 dark:text-purple-300",
    InProgress: "bg-amber-100 text-amber-700 dark:bg-amber-900 dark:text-amber-300",
    Completed: "bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300",
  };

  const labels: Record<WorkOrderStatus, string> = {
    New: "New",
    Assigned: "Assigned",
    InProgress: "In Progress",
    Completed: "Completed",
  };

  return (
    <span className={`text-xs font-medium rounded px-2 py-0.5 ${styles[status]}`}>
      {labels[status]}
    </span>
  );
}

function WorkOrderListSkeleton() {
  return (
    <div className="space-y-2 animate-pulse">
      {[1, 2, 3, 4].map((i) => (
        <div key={i} className="h-20 rounded-lg bg-gray-100 dark:bg-gray-800" />
      ))}
    </div>
  );
}
