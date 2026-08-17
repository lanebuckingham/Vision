"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { getIncidentById, updateIncidentStatus, getWorkOrders } from "@/lib/api/client";
import type { IncidentDetailDto, IncidentStatus, WorkOrderListItemDto } from "@/lib/api/types";

export default function IncidentDetailPage() {
  const params = useParams<{ id: string }>();
  const [incident, setIncident] = useState<IncidentDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [updating, setUpdating] = useState(false);
  const [showResolveForm, setShowResolveForm] = useState(false);
  const [resolutionSummary, setResolutionSummary] = useState("");

  const [linkedWorkOrder, setLinkedWorkOrder] = useState<WorkOrderListItemDto | null>(null);
  const [timedOutForIncidentId, setTimedOutForIncidentId] = useState<string | null>(null);

  useEffect(() => {
    if (!params.id) return;
    let cancelled = false;
    getIncidentById(params.id)
      .then((result) => {
        if (!cancelled) setIncident(result);
      })
      .catch((e) => {
        if (!cancelled) setError(e.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [params.id]);

  useEffect(() => {
    if (!incident) return;
    let cancelled = false;
    void (async () => {
      try {
        const result = await getWorkOrders({ incidentId: incident.id, pageSize: 1 });
        if (cancelled) return;
        if (result.items.length > 0) {
          setLinkedWorkOrder(result.items[0]);
        }
      } catch {
        // WorkOrderService may be unavailable — not critical for incident page
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [incident]);

  useEffect(() => {
    if (!incident) return;
    if (linkedWorkOrder) return;
    if (incident.severity !== "Critical" || !incident.asset) return;
    if (incident.status === "Resolved") return;

    let attempts = 0;
    const maxAttempts = 10;
    const interval = setInterval(async () => {
      attempts++;
      try {
        const result = await getWorkOrders({ incidentId: incident.id, pageSize: 1 });
        if (result.items.length > 0) {
          setLinkedWorkOrder(result.items[0]);
          clearInterval(interval);
          return;
        }
      } catch {
        // WorkOrderService may be unavailable — keep polling until exhausted
      }
      if (attempts >= maxAttempts) {
        clearInterval(interval);
        setTimedOutForIncidentId(incident.id);
      }
    }, 2000);

    return () => clearInterval(interval);
  }, [incident, linkedWorkOrder]);

  const workOrderPending =
    !!incident &&
    !linkedWorkOrder &&
    timedOutForIncidentId !== incident.id &&
    incident.severity === "Critical" &&
    !!incident.asset &&
    incident.status !== "Resolved";

  const handleTransition = async (newStatus: IncidentStatus, summary?: string) => {
    if (!incident) return;
    setUpdating(true);
    setError(null);
    try {
      const updated = await updateIncidentStatus(incident.id, {
        status: newStatus,
        resolutionSummary: summary,
      });
      setIncident(updated);
      setShowResolveForm(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to update incident");
    } finally {
      setUpdating(false);
    }
  };

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-8 w-64 rounded bg-gray-200 dark:bg-gray-700" />
        <div className="h-48 rounded-lg bg-gray-100 dark:bg-gray-800" />
      </div>
    );
  }

  if (error && !incident) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6 dark:border-red-900 dark:bg-red-950">
        <h2 className="text-lg font-semibold text-red-800 dark:text-red-200">Error</h2>
        <p className="mt-2 text-sm text-red-600 dark:text-red-400">{error}</p>
        <Link href="/incidents" className="mt-4 inline-block text-sm text-blue-600 hover:underline dark:text-blue-400">
          Back to Incidents
        </Link>
      </div>
    );
  }

  if (!incident) return null;

  const canInvestigate = incident.status === "Open";
  const canResolve = incident.status === "Open" || incident.status === "Investigating";

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <Link href="/incidents" className="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300">
          ← Back to Incidents
        </Link>
        <div className="mt-1 flex items-start justify-between gap-4">
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">{incident.title}</h1>
          <div className="flex items-center gap-2">
            <SeverityBadge severity={incident.severity} />
            <StatusBadge status={incident.status} />
          </div>
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-3 dark:border-red-900 dark:bg-red-950">
          <p className="text-sm text-red-700 dark:text-red-300">{error}</p>
        </div>
      )}

      {/* Description */}
      <div className="rounded-lg border border-gray-200 p-4 dark:border-gray-800">
        <p className="text-sm text-gray-700 dark:text-gray-300">{incident.description}</p>
      </div>

      {/* Details Grid */}
      <div className="grid gap-6 md:grid-cols-2">
        <div className="rounded-lg border border-gray-200 p-4 dark:border-gray-800">
          <h2 className="mb-3 text-sm font-semibold uppercase text-gray-500 dark:text-gray-400">Details</h2>
          <dl className="space-y-2 text-sm">
            <DetailRow label="Severity" value={incident.severity} />
            <DetailRow label="Status" value={incident.status} />
            <DetailRow label="Created" value={new Date(incident.createdAt).toLocaleString()} />
            <DetailRow label="Updated" value={new Date(incident.updatedAt).toLocaleString()} />
            {incident.resolvedAt && (
              <DetailRow label="Resolved" value={new Date(incident.resolvedAt).toLocaleString()} />
            )}
          </dl>
        </div>

        <div className="rounded-lg border border-gray-200 p-4 dark:border-gray-800">
          <h2 className="mb-3 text-sm font-semibold uppercase text-gray-500 dark:text-gray-400">Location & Asset</h2>
          <dl className="space-y-2 text-sm">
            <DetailRow label="Building" value={incident.building.name} />
            <DetailRow label="Location" value={incident.location.name} />
            {incident.location.floor && <DetailRow label="Floor" value={incident.location.floor} />}
            {incident.location.department && <DetailRow label="Department" value={incident.location.department} />}
            {incident.asset && (
              <>
                <DetailRow label="Asset" value={incident.asset.name} />
                <DetailRow label="Asset Type" value={incident.asset.assetType} />
                <DetailRow label="Asset Status" value={incident.asset.status} />
                {incident.asset.assetTag && <DetailRow label="Asset Tag" value={incident.asset.assetTag} />}
              </>
            )}
          </dl>
        </div>
      </div>

      {/* Linked Work Order */}
      {linkedWorkOrder && (
        <div className="rounded-lg border border-purple-200 bg-purple-50 p-4 dark:border-purple-900 dark:bg-purple-950">
          <h2 className="mb-2 text-sm font-semibold text-purple-800 dark:text-purple-200">Work Order</h2>
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium text-purple-900 dark:text-purple-100">{linkedWorkOrder.title}</p>
              <p className="text-xs text-purple-600 dark:text-purple-400">
                {linkedWorkOrder.status === "InProgress" ? "In Progress" : linkedWorkOrder.status}
                {linkedWorkOrder.assignedTechnician && ` · ${linkedWorkOrder.assignedTechnician.displayName}`}
              </p>
            </div>
            <Link
              href={`/work-orders/${linkedWorkOrder.id}`}
              className="rounded-lg bg-purple-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-purple-700"
            >
              View Work Order
            </Link>
          </div>
        </div>
      )}
      {workOrderPending && !linkedWorkOrder && (
        <div className="rounded-lg border border-gray-200 bg-gray-50 p-4 dark:border-gray-800 dark:bg-gray-900">
          <p className="text-sm text-gray-600 dark:text-gray-400">
            Work order is being created...
          </p>
        </div>
      )}

      {/* Resolution Summary */}
      {incident.resolutionSummary && (
        <div className="rounded-lg border border-green-200 bg-green-50 p-4 dark:border-green-900 dark:bg-green-950">
          <h2 className="mb-2 text-sm font-semibold text-green-800 dark:text-green-200">Resolution</h2>
          <p className="text-sm text-green-700 dark:text-green-300">{incident.resolutionSummary}</p>
        </div>
      )}

      {/* Actions */}
      {(canInvestigate || canResolve) && (
        <div className="rounded-lg border border-gray-200 p-4 dark:border-gray-800">
          <h2 className="mb-3 text-sm font-semibold uppercase text-gray-500 dark:text-gray-400">Actions</h2>
          <div className="flex flex-wrap gap-3">
            {canInvestigate && (
              <button
                onClick={() => handleTransition("Investigating")}
                disabled={updating}
                className="rounded-lg bg-yellow-600 px-4 py-2 text-sm font-medium text-white hover:bg-yellow-700 disabled:opacity-50"
              >
                {updating ? "Updating..." : "Start Investigation"}
              </button>
            )}
            {canResolve && !showResolveForm && (
              <button
                onClick={() => setShowResolveForm(true)}
                className="rounded-lg bg-green-600 px-4 py-2 text-sm font-medium text-white hover:bg-green-700"
              >
                Resolve
              </button>
            )}
          </div>

          {showResolveForm && (
            <div className="mt-4 space-y-3">
              <label htmlFor="resolution" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
                Resolution Summary
              </label>
              <textarea
                id="resolution"
                value={resolutionSummary}
                onChange={(e) => setResolutionSummary(e.target.value)}
                rows={3}
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
                placeholder="Describe how the incident was resolved..."
              />
              <div className="flex gap-2">
                <button
                  onClick={() => handleTransition("Resolved", resolutionSummary)}
                  disabled={updating || !resolutionSummary.trim()}
                  className="rounded-lg bg-green-600 px-4 py-2 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50"
                >
                  {updating ? "Resolving..." : "Confirm Resolution"}
                </button>
                <button
                  onClick={() => setShowResolveForm(false)}
                  className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-800"
                >
                  Cancel
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function SeverityBadge({ severity }: { severity: string }) {
  const styles: Record<string, string> = {
    Critical: "bg-red-100 text-red-700 dark:bg-red-900 dark:text-red-300",
    High: "bg-orange-100 text-orange-700 dark:bg-orange-900 dark:text-orange-300",
    Medium: "bg-yellow-100 text-yellow-700 dark:bg-yellow-900 dark:text-yellow-300",
    Low: "bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300",
  };
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${styles[severity] || styles.Low}`}>
      {severity}
    </span>
  );
}

function StatusBadge({ status }: { status: IncidentStatus }) {
  const styles = {
    Open: "bg-blue-100 text-blue-700 dark:bg-blue-900 dark:text-blue-300",
    Investigating: "bg-yellow-100 text-yellow-700 dark:bg-yellow-900 dark:text-yellow-300",
    Resolved: "bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300",
  }[status];

  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${styles}`}>
      {status}
    </span>
  );
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between">
      <dt className="text-gray-500 dark:text-gray-400">{label}</dt>
      <dd className="font-medium text-gray-900 dark:text-white">{value}</dd>
    </div>
  );
}
