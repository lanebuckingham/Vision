"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { getDashboard, getWorkOrderSummary, getCredentialSummary } from "@/lib/api/client";
import type { SecurityDashboardDto, WorkOrderSummaryDto, CredentialSummaryDto } from "@/lib/api/types";

export default function DashboardPage() {
  const [data, setData] = useState<SecurityDashboardDto | null>(null);
  const [woSummary, setWoSummary] = useState<WorkOrderSummaryDto | null>(null);
  const [credSummary, setCredSummary] = useState<CredentialSummaryDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      getDashboard(),
      getWorkOrderSummary().catch(() => null),
      getCredentialSummary().catch(() => null),
    ])
      .then(([dashboardData, workOrderData, credentialData]) => {
        setData(dashboardData);
        setWoSummary(workOrderData);
        setCredSummary(credentialData);
      })
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return <DashboardSkeleton />;
  }

  if (error) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6 dark:border-red-900 dark:bg-red-950">
        <h2 className="text-lg font-semibold text-red-800 dark:text-red-200">Unable to load dashboard</h2>
        <p className="mt-2 text-sm text-red-600 dark:text-red-400">{error}</p>
      </div>
    );
  }

  if (!data) return null;

  const { hospital, securityHealth, incidents, criticalAlerts, recentActivity } = data;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">{hospital.name}</h1>
        <p className="text-sm text-gray-500 dark:text-gray-400">Security Operations Dashboard</p>
      </div>

      {/* Security Health */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard
          label="Security Health"
          value={`${securityHealth.operationalPercentage}%`}
          detail={`${securityHealth.operationalAssets} of ${securityHealth.totalAssets} operational`}
          status={securityHealth.operationalPercentage >= 95 ? "good" : securityHealth.operationalPercentage >= 85 ? "warning" : "critical"}
        />
        <StatCard
          label="Degraded Assets"
          value={securityHealth.degradedAssets.toString()}
          detail="Partially functioning"
          status={securityHealth.degradedAssets === 0 ? "good" : "warning"}
        />
        <StatCard
          label="Offline Assets"
          value={securityHealth.offlineAssets.toString()}
          detail="Nonfunctional"
          status={securityHealth.offlineAssets === 0 ? "good" : "critical"}
        />
        <StatCard
          label="Active Incidents"
          value={incidents.activeTotal.toString()}
          detail={`${incidents.activeCritical} critical`}
          status={incidents.activeCritical === 0 ? "good" : "critical"}
        />
      </div>

      {/* Work Orders */}
      {woSummary && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Link href="/work-orders" className="block">
            <StatCard
              label="Open Work Orders"
              value={woSummary.openCount.toString()}
              detail={`${woSummary.byStatus.new} new · ${woSummary.byStatus.inProgress} in progress`}
              status={woSummary.openCount === 0 ? "good" : woSummary.openCount <= 3 ? "warning" : "critical"}
            />
          </Link>
        </div>
      )}

      {/* Credentials */}
      {credSummary && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Link href="/credentials" className="block">
            <StatCard
              label="Expiring Credentials"
              value={credSummary.expiringSoonCount.toString()}
              detail={`${credSummary.activeCount} active total`}
              status={credSummary.expiringSoonCount === 0 ? "good" : credSummary.expiringSoonCount <= 2 ? "warning" : "critical"}
            />
          </Link>
        </div>
      )}

      {/* Critical Alerts */}
      <section>
        <h2 className="mb-3 text-lg font-semibold text-gray-900 dark:text-white">
          Critical Alerts
        </h2>
        {criticalAlerts.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No active critical alerts.</p>
        ) : (
          <div className="space-y-2">
            {criticalAlerts.map((alert) => (
              <Link
                key={alert.incidentId}
                href={`/incidents/${alert.incidentId}`}
                className="flex items-start gap-3 rounded-lg border border-red-200 bg-red-50 p-4 transition-colors hover:bg-red-100 dark:border-red-900 dark:bg-red-950 dark:hover:bg-red-900"
              >
                <span className="mt-0.5 text-red-600 dark:text-red-400" aria-label="Critical alert">●</span>
                <div className="flex-1 min-w-0">
                  <p className="font-medium text-red-800 dark:text-red-200">{alert.title}</p>
                  <p className="text-sm text-red-600 dark:text-red-400">
                    {alert.assetName && `${alert.assetName} · `}{alert.locationName}
                  </p>
                </div>
                <time className="text-xs text-red-500 dark:text-red-500 whitespace-nowrap">
                  {formatRelativeTime(alert.createdAt)}
                </time>
              </Link>
            ))}
          </div>
        )}
      </section>

      {/* Recent Activity */}
      <section>
        <h2 className="mb-3 text-lg font-semibold text-gray-900 dark:text-white">
          Recent Activity
        </h2>
        {recentActivity.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No recent activity.</p>
        ) : (
          <div className="divide-y divide-gray-100 rounded-lg border border-gray-200 dark:divide-gray-800 dark:border-gray-800">
            {recentActivity.map((activity, idx) => (
              <div key={idx} className="flex items-center gap-3 px-4 py-3">
                <span className={`text-xs font-medium rounded px-2 py-0.5 ${
                  activity.type === "IncidentResolved"
                    ? "bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300"
                    : "bg-blue-100 text-blue-700 dark:bg-blue-900 dark:text-blue-300"
                }`}>
                  {activity.type === "IncidentResolved" ? "Resolved" : "Created"}
                </span>
                <p className="flex-1 text-sm text-gray-700 dark:text-gray-300 truncate">
                  {activity.title}
                </p>
                <time className="text-xs text-gray-500 dark:text-gray-500 whitespace-nowrap">
                  {formatRelativeTime(activity.occurredAt)}
                </time>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function StatCard({ label, value, detail, status }: {
  label: string;
  value: string;
  detail: string;
  status: "good" | "warning" | "critical";
}) {
  const borderColor = {
    good: "border-green-200 dark:border-green-900",
    warning: "border-yellow-200 dark:border-yellow-900",
    critical: "border-red-200 dark:border-red-900",
  }[status];

  const valueColor = {
    good: "text-green-700 dark:text-green-400",
    warning: "text-yellow-700 dark:text-yellow-400",
    critical: "text-red-700 dark:text-red-400",
  }[status];

  return (
    <div className={`rounded-lg border p-4 ${borderColor}`}>
      <p className="text-sm font-medium text-gray-600 dark:text-gray-400">{label}</p>
      <p className={`mt-1 text-2xl font-bold ${valueColor}`}>{value}</p>
      <p className="mt-1 text-xs text-gray-500 dark:text-gray-500">{detail}</p>
    </div>
  );
}

function DashboardSkeleton() {
  return (
    <div className="space-y-6 animate-pulse">
      <div className="h-8 w-64 rounded bg-gray-200 dark:bg-gray-700" />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {[1, 2, 3, 4].map((i) => (
          <div key={i} className="h-24 rounded-lg bg-gray-100 dark:bg-gray-800" />
        ))}
      </div>
      <div className="h-40 rounded-lg bg-gray-100 dark:bg-gray-800" />
    </div>
  );
}

function formatRelativeTime(iso: string): string {
  const date = new Date(iso);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);

  if (diffMins < 1) return "just now";
  if (diffMins < 60) return `${diffMins}m ago`;
  const diffHours = Math.floor(diffMins / 60);
  if (diffHours < 24) return `${diffHours}h ago`;
  const diffDays = Math.floor(diffHours / 24);
  if (diffDays < 7) return `${diffDays}d ago`;
  return date.toLocaleDateString();
}
