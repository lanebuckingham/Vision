"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { getAssetById } from "@/lib/api/client";
import type { AssetDetailDto, AssetStatus } from "@/lib/api/types";

export default function AssetDetailPage() {
  const params = useParams<{ id: string }>();
  const [asset, setAsset] = useState<AssetDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!params.id) return;
    getAssetById(params.id)
      .then(setAsset)
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, [params.id]);

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-8 w-64 rounded bg-gray-200 dark:bg-gray-700" />
        <div className="h-48 rounded-lg bg-gray-100 dark:bg-gray-800" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6 dark:border-red-900 dark:bg-red-950">
        <h2 className="text-lg font-semibold text-red-800 dark:text-red-200">Error</h2>
        <p className="mt-2 text-sm text-red-600 dark:text-red-400">{error}</p>
        <Link href="/assets" className="mt-4 inline-block text-sm text-blue-600 hover:underline dark:text-blue-400">
          Back to Assets
        </Link>
      </div>
    );
  }

  if (!asset) return null;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <Link href="/assets" className="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300">
            ← Back to Assets
          </Link>
          <h1 className="mt-1 text-2xl font-bold text-gray-900 dark:text-white">{asset.name}</h1>
          {asset.assetTag && (
            <p className="text-sm text-gray-500 dark:text-gray-400">{asset.assetTag}</p>
          )}
        </div>
        <StatusBadge status={asset.status} />
      </div>

      {/* Details */}
      <div className="grid gap-6 md:grid-cols-2">
        <div className="rounded-lg border border-gray-200 p-4 dark:border-gray-800">
          <h2 className="mb-3 text-sm font-semibold uppercase text-gray-500 dark:text-gray-400">Asset Details</h2>
          <dl className="space-y-2 text-sm">
            <DetailRow label="Type" value={asset.assetType} />
            <DetailRow label="Status" value={asset.status} />
            {asset.manufacturer && <DetailRow label="Manufacturer" value={asset.manufacturer} />}
            {asset.model && <DetailRow label="Model" value={asset.model} />}
            {asset.description && <DetailRow label="Description" value={asset.description} />}
            {asset.lastServiceAt && <DetailRow label="Last Service" value={new Date(asset.lastServiceAt).toLocaleDateString()} />}
            {asset.statusChangedAt && <DetailRow label="Status Since" value={new Date(asset.statusChangedAt).toLocaleString()} />}
          </dl>
        </div>

        <div className="rounded-lg border border-gray-200 p-4 dark:border-gray-800">
          <h2 className="mb-3 text-sm font-semibold uppercase text-gray-500 dark:text-gray-400">Location</h2>
          <dl className="space-y-2 text-sm">
            <DetailRow label="Building" value={asset.building.name} />
            <DetailRow label="Location" value={asset.location.name} />
            {asset.location.floor && <DetailRow label="Floor" value={asset.location.floor} />}
            {asset.location.department && <DetailRow label="Department" value={asset.location.department} />}
          </dl>
        </div>
      </div>

      {/* Recent Incidents */}
      <section>
        <h2 className="mb-3 text-lg font-semibold text-gray-900 dark:text-white">Recent Incidents</h2>
        {asset.recentIncidents.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No recent incidents for this asset.</p>
        ) : (
          <div className="divide-y divide-gray-100 rounded-lg border border-gray-200 dark:divide-gray-800 dark:border-gray-800">
            {asset.recentIncidents.map((incident) => (
              <Link
                key={incident.id}
                href={`/incidents/${incident.id}`}
                className="flex items-center justify-between px-4 py-3 hover:bg-gray-50 dark:hover:bg-gray-900"
              >
                <div>
                  <p className="text-sm font-medium text-gray-900 dark:text-white">{incident.title}</p>
                  <p className="text-xs text-gray-500 dark:text-gray-400">
                    {incident.severity} · {incident.status} · {new Date(incident.createdAt).toLocaleDateString()}
                  </p>
                </div>
                <SeverityBadge severity={incident.severity} />
              </Link>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function StatusBadge({ status }: { status: AssetStatus }) {
  const styles = {
    Operational: "bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300",
    Degraded: "bg-yellow-100 text-yellow-700 dark:bg-yellow-900 dark:text-yellow-300",
    Offline: "bg-red-100 text-red-700 dark:bg-red-900 dark:text-red-300",
  }[status];

  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${styles}`}>
      {status}
    </span>
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
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${styles[severity] || styles.Low}`}>
      {severity}
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
