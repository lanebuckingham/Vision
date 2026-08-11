"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { createIncident, getAssets } from "@/lib/api/client";
import type { AssetListItemDto, IncidentSeverity } from "@/lib/api/types";

const SEVERITY_OPTIONS: IncidentSeverity[] = ["Critical", "High", "Medium", "Low"];

export default function CreateIncidentPage() {
  const router = useRouter();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [assets, setAssets] = useState<AssetListItemDto[]>([]);

  // Form state
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [severity, setSeverity] = useState<IncidentSeverity>("Medium");
  const [selectedAssetId, setSelectedAssetId] = useState("");

  // Load assets for selection
  useEffect(() => {
    getAssets({ pageSize: 100 })
      .then((data) => setAssets(data.items))
      .catch(() => {});
  }, []);

  const selectedAsset = assets.find((a) => a.id === selectedAssetId);
  const locationId = selectedAsset?.location.id;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!locationId) {
      setError("Please select an asset to determine the location.");
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const result = await createIncident({
        locationId,
        securityAssetId: selectedAssetId || undefined,
        title,
        description,
        severity,
      });
      router.push(`/incidents/${result.id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to create incident");
      setSubmitting(false);
    }
  };

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div>
        <Link href="/incidents" className="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300">
          ← Back to Incidents
        </Link>
        <h1 className="mt-1 text-2xl font-bold text-gray-900 dark:text-white">Create Security Incident</h1>
      </div>

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 dark:border-red-900 dark:bg-red-950">
          <p className="text-sm text-red-700 dark:text-red-300">{error}</p>
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        {/* Asset selection */}
        <div>
          <label htmlFor="asset" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
            Affected Asset
          </label>
          <select
            id="asset"
            value={selectedAssetId}
            onChange={(e) => setSelectedAssetId(e.target.value)}
            className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
          >
            <option value="">Select an asset...</option>
            {assets.map((asset) => (
              <option key={asset.id} value={asset.id}>
                {asset.name} — {asset.location.name} ({asset.building.name})
              </option>
            ))}
          </select>
          {selectedAsset && (
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              Location: {selectedAsset.location.name}, {selectedAsset.building.name}
            </p>
          )}
        </div>

        {/* Severity */}
        <div>
          <label htmlFor="severity" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
            Severity
          </label>
          <select
            id="severity"
            value={severity}
            onChange={(e) => setSeverity(e.target.value as IncidentSeverity)}
            className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
          >
            {SEVERITY_OPTIONS.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>

        {/* Title */}
        <div>
          <label htmlFor="title" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
            Title
          </label>
          <input
            id="title"
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            maxLength={150}
            required
            className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
            placeholder="Brief description of the incident"
          />
        </div>

        {/* Description */}
        <div>
          <label htmlFor="description" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
            Description
          </label>
          <textarea
            id="description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            maxLength={2000}
            required
            rows={4}
            className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
            placeholder="Detailed description of the security incident..."
          />
        </div>

        {/* Submit */}
        <div className="flex gap-3 pt-2">
          <button
            type="submit"
            disabled={submitting || !title.trim() || !description.trim() || !selectedAssetId}
            className="rounded-lg bg-blue-600 px-6 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
          >
            {submitting ? "Creating..." : "Create Incident"}
          </button>
          <Link
            href="/incidents"
            className="rounded-lg border border-gray-300 px-6 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-800"
          >
            Cancel
          </Link>
        </div>
      </form>
    </div>
  );
}
