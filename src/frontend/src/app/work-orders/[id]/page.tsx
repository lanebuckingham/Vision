"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { useAuth } from "@/lib/auth/AuthContext";
import {
  getWorkOrderById,
  assignTechnician,
  startWork,
  addTechnicianNote,
  completeWorkOrder,
  getTechnicians,
  updateAssetStatus,
  updateIncidentStatus,
} from "@/lib/api/client";
import type {
  WorkOrderDetailDto,
  WorkOrderStatus,
  WorkOrderPriority,
  TechnicianListItemDto,
} from "@/lib/api/types";

export default function WorkOrderDetailPage() {
  const params = useParams();
  const { hasRole } = useAuth();
  const id = params.id as string;

  const [wo, setWo] = useState<WorkOrderDetailDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);

  // Assignment state
  const [technicians, setTechnicians] = useState<TechnicianListItemDto[]>([]);
  const [techniciansError, setTechniciansError] = useState<string | null>(null);
  const [selectedTechnicianId, setSelectedTechnicianId] = useState("");
  const [showAssign, setShowAssign] = useState(false);

  // Note state
  const [noteContent, setNoteContent] = useState("");
  const [showNoteForm, setShowNoteForm] = useState(false);

  // Completion state
  const [completionSummary, setCompletionSummary] = useState("");
  const [showCompleteForm, setShowCompleteForm] = useState(false);

  useEffect(() => {
    let cancelled = false;
    getWorkOrderById(id)
      .then((result) => {
        if (!cancelled) setWo(result);
      })
      .catch((e) => {
        if (!cancelled) setError(e instanceof Error ? e.message : "Failed to load work order");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [id]);

  const loadTechnicians = () => {
    setTechniciansError(null);
    getTechnicians({ activeOnly: true, pageSize: 50 })
      .then((data) => setTechnicians(data.items))
      .catch((e) => setTechniciansError(e instanceof Error ? e.message : "Failed to load technicians"));
  };

  const handleAssign = async () => {
    if (!selectedTechnicianId) return;
    setActionLoading(true);
    setActionError(null);
    try {
      const updated = await assignTechnician(id, { technicianId: selectedTechnicianId });
      setWo(updated);
      setShowAssign(false);
      setSelectedTechnicianId("");
    } catch (e) {
      setActionError(e instanceof Error ? e.message : "Assignment failed");
    } finally {
      setActionLoading(false);
    }
  };

  const handleStart = async () => {
    setActionLoading(true);
    setActionError(null);
    try {
      const updated = await startWork(id);
      setWo(updated);
    } catch (e) {
      setActionError(e instanceof Error ? e.message : "Failed to start work");
    } finally {
      setActionLoading(false);
    }
  };

  const handleAddNote = async () => {
    if (!noteContent.trim()) return;
    setActionLoading(true);
    setActionError(null);
    try {
      await addTechnicianNote(id, { content: noteContent });
      // Reload to get updated notes list
      const updated = await getWorkOrderById(id);
      setWo(updated);
      setNoteContent("");
      setShowNoteForm(false);
    } catch (e) {
      setActionError(e instanceof Error ? e.message : "Failed to add note");
    } finally {
      setActionLoading(false);
    }
  };

  const handleComplete = async () => {
    setActionLoading(true);
    setActionError(null);
    try {
      const updated = await completeWorkOrder(id, {
        completionSummary: completionSummary.trim() || undefined,
      });
      setWo(updated);
      setShowCompleteForm(false);
      setCompletionSummary("");
      // Technician completion stops here.
      // Security resolution (asset → Operational, incident → Resolved) is a SecurityManager action
      // performed via "Finish Security Resolution" by a SecurityManager user.
    } catch (e) {
      setActionError(e instanceof Error ? e.message : "Failed to complete work order");
    } finally {
      setActionLoading(false);
    }
  };

  const handleRetrySecurityUpdate = async () => {
    if (!wo || !wo.securityAssetId || !wo.securityIncidentId) return;
    setActionLoading(true);
    setActionError(null);
    try {
      await updateAssetStatus(wo.securityAssetId, "Operational");
      await updateIncidentStatus(wo.securityIncidentId, {
        status: "Resolved",
        resolutionSummary: `Repair completed: ${wo.completionSummary || "See work order notes."}`,
      });
      setActionError(null);
      // Refresh to confirm state
      const updated = await getWorkOrderById(id);
      setWo(updated);
    } catch (e) {
      setActionError(
        "Security status update failed. " +
        (e instanceof Error ? e.message : "Please try again.")
      );
    } finally {
      setActionLoading(false);
    }
  };

  const waitingForCurrentOrder = loading || (wo != null && wo.id !== id);

  if (waitingForCurrentOrder) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-8 w-48 rounded bg-gray-200 dark:bg-gray-700" />
        <div className="h-64 rounded-lg bg-gray-100 dark:bg-gray-800" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6 dark:border-red-900 dark:bg-red-950">
        <h2 className="text-lg font-semibold text-red-800 dark:text-red-200">Unable to load work order</h2>
        <p className="mt-2 text-sm text-red-600 dark:text-red-400">{error}</p>
        <Link href="/work-orders" className="mt-4 inline-block text-sm text-blue-600 hover:text-blue-700 dark:text-blue-400">
          ← Back to Work Orders
        </Link>
      </div>
    );
  }

  if (!wo) return null;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <Link href="/work-orders" className="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300">
          ← Back to Work Orders
        </Link>
        <h1 className="mt-1 text-2xl font-bold text-gray-900 dark:text-white">{wo.title}</h1>
        <div className="mt-2 flex items-center gap-2">
          <PriorityBadge priority={wo.priority} />
          <StatusBadge status={wo.status} />
        </div>
      </div>

      {/* Action error */}
      {actionError && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 dark:border-red-900 dark:bg-red-950">
          <p className="text-sm text-red-700 dark:text-red-300">{actionError}</p>
        </div>
      )}

      {/* Info grid */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <InfoCard label="Asset" value={wo.assetName || "—"} />
        <InfoCard label="Location" value={wo.locationName || "—"} />
        <InfoCard label="Technician" value={wo.assignedTechnician?.displayName || "Unassigned"} />
        {wo.assignedAt && <InfoCard label="Assigned" value={formatDate(wo.assignedAt)} />}
        {wo.startedAt && <InfoCard label="Started" value={formatDate(wo.startedAt)} />}
        {wo.completedAt && <InfoCard label="Completed" value={formatDate(wo.completedAt)} />}
        <InfoCard label="Created" value={formatDate(wo.createdAt)} />
        {wo.securityIncidentId && (
          <InfoCard label="Source Incident" value={
            <Link href={`/incidents/${wo.securityIncidentId}`} className="text-blue-600 hover:text-blue-700 dark:text-blue-400">
              View Incident
            </Link>
          } />
        )}
      </div>

      {/* Description */}
      <section>
        <h2 className="text-sm font-medium text-gray-600 dark:text-gray-400">Description</h2>
        <p className="mt-1 text-gray-900 dark:text-gray-100 whitespace-pre-wrap">{wo.description}</p>
      </section>

      {/* Completion summary */}
      {wo.completionSummary && (
        <section>
          <h2 className="text-sm font-medium text-gray-600 dark:text-gray-400">Completion Summary</h2>
          <p className="mt-1 text-gray-900 dark:text-gray-100 whitespace-pre-wrap">{wo.completionSummary}</p>
        </section>
      )}

      {/* Lifecycle actions */}
      <section className="space-y-3">
        {wo.status === "New" && hasRole("SecurityManager") && (
          <div>
            {!showAssign ? (
              <button
                onClick={() => { setShowAssign(true); loadTechnicians(); }}
                className="rounded-lg bg-purple-600 px-4 py-2 text-sm font-medium text-white hover:bg-purple-700"
              >
                Assign Technician
              </button>
            ) : (
              <div className="flex items-end gap-3 rounded-lg border border-gray-200 p-4 dark:border-gray-800">
                <div className="flex-1">
                  <label htmlFor="technician-select" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
                    Select Technician
                  </label>
                  <select
                    id="technician-select"
                    value={selectedTechnicianId}
                    onChange={(e) => setSelectedTechnicianId(e.target.value)}
                    disabled={!!techniciansError}
                    className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
                  >
                    <option value="">Choose a technician...</option>
                    {technicians.map((t) => (
                      <option key={t.id} value={t.id}>
                        {t.displayName}{t.specialty ? ` — ${t.specialty}` : ""}
                      </option>
                    ))}
                  </select>
                  {techniciansError && (
                    <div className="mt-2 flex items-center gap-2">
                      <p className="text-sm text-red-600 dark:text-red-400">{techniciansError}</p>
                      <button
                        type="button"
                        onClick={loadTechnicians}
                        className="text-sm font-medium text-blue-600 hover:text-blue-700 dark:text-blue-400 dark:hover:text-blue-300"
                      >
                        Retry
                      </button>
                    </div>
                  )}
                </div>
                <button
                  onClick={handleAssign}
                  disabled={!selectedTechnicianId || actionLoading}
                  className="rounded-lg bg-purple-600 px-4 py-2 text-sm font-medium text-white hover:bg-purple-700 disabled:opacity-50"
                >
                  {actionLoading ? "Assigning..." : "Assign"}
                </button>
                <button
                  onClick={() => setShowAssign(false)}
                  className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300"
                >
                  Cancel
                </button>
              </div>
            )}
          </div>
        )}

        {wo.status === "Assigned" && hasRole("Technician") && (
          <button
            onClick={handleStart}
            disabled={actionLoading}
            className="rounded-lg bg-amber-600 px-4 py-2 text-sm font-medium text-white hover:bg-amber-700 disabled:opacity-50"
          >
            {actionLoading ? "Starting..." : "Start Work"}
          </button>
        )}

        {wo.status === "InProgress" && hasRole("Technician") && (
          <div className="flex flex-wrap gap-3">
            <button
              onClick={() => setShowNoteForm(true)}
              className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
            >
              Add Repair Note
            </button>
            <button
              onClick={() => setShowCompleteForm(true)}
              className="rounded-lg bg-green-600 px-4 py-2 text-sm font-medium text-white hover:bg-green-700"
            >
              Complete Work
            </button>
          </div>
        )}

        {wo.status === "Completed" && wo.securityIncidentId && wo.securityAssetId && hasRole("SecurityManager") && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 p-4 dark:border-amber-900 dark:bg-amber-950">
            <h3 className="text-sm font-semibold text-amber-800 dark:text-amber-200">Security Status Update</h3>
            <p className="mt-1 text-sm text-amber-700 dark:text-amber-300">
              Mark the asset as operational and resolve the related incident to update dashboard health.
            </p>
            <div className="mt-3 flex flex-wrap gap-3">
              <button
                onClick={handleRetrySecurityUpdate}
                disabled={actionLoading}
                className="rounded-lg bg-amber-600 px-4 py-2 text-sm font-medium text-white hover:bg-amber-700 disabled:opacity-50"
              >
                {actionLoading ? "Updating..." : "Finish Security Resolution"}
              </button>
              <Link
                href="/dashboard"
                className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-800"
              >
                Return to Dashboard
              </Link>
            </div>
          </div>
        )}
      </section>

      {/* Add note form */}
      {showNoteForm && (
        <div className="rounded-lg border border-gray-200 p-4 dark:border-gray-800">
          <label htmlFor="note-content" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
            Repair Note
          </label>
          <textarea
            id="note-content"
            value={noteContent}
            onChange={(e) => setNoteContent(e.target.value)}
            maxLength={2000}
            rows={3}
            className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
            placeholder="Describe the repair work performed..."
          />
          <div className="mt-3 flex gap-2">
            <button
              onClick={handleAddNote}
              disabled={!noteContent.trim() || actionLoading}
              className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {actionLoading ? "Adding..." : "Add Note"}
            </button>
            <button
              onClick={() => { setShowNoteForm(false); setNoteContent(""); }}
              className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300"
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      {/* Complete form */}
      {showCompleteForm && (
        <div className="rounded-lg border border-gray-200 p-4 dark:border-gray-800">
          <label htmlFor="completion-summary" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
            Completion Summary {wo.notes.length > 0 && "(optional if notes exist)"}
          </label>
          <textarea
            id="completion-summary"
            value={completionSummary}
            onChange={(e) => setCompletionSummary(e.target.value)}
            maxLength={2000}
            rows={3}
            className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
            placeholder="Summary of the completed repair..."
          />
          <div className="mt-3 flex gap-2">
            <button
              onClick={handleComplete}
              disabled={actionLoading}
              className="rounded-lg bg-green-600 px-4 py-2 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50"
            >
              {actionLoading ? "Completing..." : "Complete Work Order"}
            </button>
            <button
              onClick={() => { setShowCompleteForm(false); setCompletionSummary(""); }}
              className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300"
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      {/* Technician Notes */}
      <section>
        <h2 className="mb-3 text-lg font-semibold text-gray-900 dark:text-white">Repair Notes</h2>
        {wo.notes.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No repair notes yet.</p>
        ) : (
          <div className="divide-y divide-gray-100 rounded-lg border border-gray-200 dark:divide-gray-800 dark:border-gray-800">
            {wo.notes.map((note) => (
              <div key={note.id} className="px-4 py-3">
                <div className="flex items-center justify-between">
                  <p className="text-sm font-medium text-gray-700 dark:text-gray-300">
                    {note.technicianDisplayName}
                  </p>
                  <time className="text-xs text-gray-500 dark:text-gray-500">
                    {formatDate(note.createdAt)}
                  </time>
                </div>
                <p className="mt-1 text-sm text-gray-900 dark:text-gray-100 whitespace-pre-wrap">{note.content}</p>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function InfoCard({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="rounded-lg border border-gray-200 p-3 dark:border-gray-800">
      <p className="text-xs font-medium text-gray-500 dark:text-gray-400">{label}</p>
      <p className="mt-1 text-sm font-medium text-gray-900 dark:text-white">{typeof value === "string" ? value : value}</p>
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

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}
