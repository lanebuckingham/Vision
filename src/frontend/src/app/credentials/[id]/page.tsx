"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { getCredentialById, revokeCredential } from "@/lib/api/client";
import { ApiError } from "@/lib/api/client";
import type { CredentialDetailDto, CredentialStatus, CredentialAccessLevel } from "@/lib/api/types";

export default function CredentialDetailPage() {
  const params = useParams<{ id: string }>();
  const [credential, setCredential] = useState<CredentialDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Revoke state
  const [showRevoke, setShowRevoke] = useState(false);
  const [revokeReason, setRevokeReason] = useState("");
  const [revokeLoading, setRevokeLoading] = useState(false);
  const [revokeError, setRevokeError] = useState<string | null>(null);

  useEffect(() => {
    if (!params.id) return;
    getCredentialById(params.id)
      .then(setCredential)
      .catch((e) => setError(e instanceof Error ? e.message : "Failed to load credential"))
      .finally(() => setLoading(false));
  }, [params.id]);

  const handleRevoke = async () => {
    if (!params.id || !revokeReason.trim()) return;
    setRevokeLoading(true);
    setRevokeError(null);
    try {
      const updated = await revokeCredential(params.id, { reason: revokeReason.trim() });
      setCredential(updated);
      setShowRevoke(false);
      setRevokeReason("");
    } catch (e) {
      if (e instanceof ApiError) {
        setRevokeError(e.message);
      } else {
        setRevokeError(e instanceof Error ? e.message : "Failed to revoke credential");
      }
    } finally {
      setRevokeLoading(false);
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

  if (error) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6 dark:border-red-900 dark:bg-red-950">
        <h2 className="text-lg font-semibold text-red-800 dark:text-red-200">Error</h2>
        <p className="mt-2 text-sm text-red-600 dark:text-red-400">{error}</p>
        <Link href="/credentials" className="mt-4 inline-block text-sm text-blue-600 hover:underline dark:text-blue-400">
          Back to Credential Management
        </Link>
      </div>
    );
  }

  if (!credential) return null;

  const canRevoke = credential.status !== "Revoked";

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <Link href="/credentials" className="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300">
            ← Credential Management
          </Link>
          <h1 className="mt-1 text-2xl font-bold text-gray-900 dark:text-white">{credential.credentialNumber}</h1>
          <div className="mt-1 flex items-center gap-2">
            <StatusBadge status={credential.status} isExpiringSoon={credential.isExpiringSoon} />
            <AccessLevelBadge level={credential.accessLevel} />
          </div>
        </div>
        {canRevoke && (
          <button
            onClick={() => setShowRevoke(true)}
            className="rounded-lg border border-red-300 bg-white px-4 py-2 text-sm font-medium text-red-700 hover:bg-red-50 dark:border-red-800 dark:bg-gray-900 dark:text-red-400 dark:hover:bg-red-950"
          >
            Revoke
          </button>
        )}
      </div>

      {/* Revoke form */}
      {showRevoke && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 dark:border-red-900 dark:bg-red-950">
          <h3 className="text-sm font-semibold text-red-800 dark:text-red-200">
            Revoke {credential.credentialNumber} for {credential.person.displayName}?
          </h3>
          <p className="mt-1 text-sm text-red-700 dark:text-red-300">
            This action is permanent. The credential will be immediately revoked.
          </p>
          {revokeError && (
            <p className="mt-2 text-sm text-red-600 dark:text-red-400">{revokeError}</p>
          )}
          <div className="mt-3">
            <label htmlFor="revoke-reason" className="block text-xs font-medium text-gray-700 dark:text-gray-300">
              Reason for revocation
            </label>
            <input
              id="revoke-reason"
              type="text"
              value={revokeReason}
              onChange={(e) => setRevokeReason(e.target.value)}
              maxLength={500}
              placeholder="Badge reported lost"
              className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
              autoFocus
            />
          </div>
          <div className="mt-4 flex gap-2">
            <button
              onClick={handleRevoke}
              disabled={!revokeReason.trim() || revokeLoading}
              className="rounded-lg bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
            >
              {revokeLoading ? "Revoking..." : "Confirm Revoke"}
            </button>
            <button
              onClick={() => { setShowRevoke(false); setRevokeReason(""); setRevokeError(null); }}
              className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300"
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      {/* Details grid */}
      <div className="grid gap-6 md:grid-cols-2">
        <div className="rounded-lg border border-gray-200 p-4 dark:border-gray-800">
          <h2 className="mb-3 text-sm font-semibold uppercase text-gray-500 dark:text-gray-400">Credential Details</h2>
          <dl className="space-y-2 text-sm">
            <DetailRow label="Number" value={credential.credentialNumber} />
            <DetailRow label="Access Level" value={credential.accessLevel} />
            <DetailRow label="Status" value={credential.isExpiringSoon && credential.status === "Active" ? "Expiring Soon" : credential.status} />
            <DetailRow label="Issued" value={new Date(credential.issuedAt).toLocaleDateString()} />
            <DetailRow label="Expires" value={new Date(credential.expiresAt).toLocaleDateString()} />
            {credential.revokedAt && (
              <>
                <DetailRow label="Revoked" value={new Date(credential.revokedAt).toLocaleString()} />
                {credential.revocationReason && (
                  <DetailRow label="Reason" value={credential.revocationReason} />
                )}
              </>
            )}
          </dl>
        </div>

        <div className="rounded-lg border border-gray-200 p-4 dark:border-gray-800">
          <h2 className="mb-3 text-sm font-semibold uppercase text-gray-500 dark:text-gray-400">Credential Holder</h2>
          <dl className="space-y-2 text-sm">
            <div className="flex justify-between">
              <dt className="text-gray-500 dark:text-gray-400">Name</dt>
              <dd>
                <Link href={`/people/${credential.person.id}`} className="font-medium text-blue-600 hover:underline dark:text-blue-400">
                  {credential.person.displayName}
                </Link>
              </dd>
            </div>
            <DetailRow label="Type" value={credential.person.personType} />
            {credential.person.employeeNumber && <DetailRow label="Personnel ID" value={credential.person.employeeNumber} />}
            {credential.person.department && <DetailRow label="Department" value={credential.person.department} />}
            {credential.person.jobTitle && <DetailRow label="Job Title" value={credential.person.jobTitle} />}
            {credential.person.email && <DetailRow label="Email" value={credential.person.email} />}
            <DetailRow label="Active" value={credential.person.isActive ? "Yes" : "No"} />
          </dl>
        </div>
      </div>
    </div>
  );
}

function StatusBadge({ status, isExpiringSoon }: { status: CredentialStatus; isExpiringSoon: boolean }) {
  if (status === "Active" && isExpiringSoon) {
    return (
      <span className="inline-flex items-center rounded-full bg-yellow-100 px-2.5 py-1 text-xs font-medium text-yellow-700 dark:bg-yellow-900 dark:text-yellow-300">
        Expiring Soon
      </span>
    );
  }

  const styles: Record<CredentialStatus, string> = {
    Active: "bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300",
    Expired: "bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300",
    Revoked: "bg-red-100 text-red-700 dark:bg-red-900 dark:text-red-300",
  };

  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${styles[status]}`}>
      {status}
    </span>
  );
}

function AccessLevelBadge({ level }: { level: CredentialAccessLevel }) {
  const styles: Record<CredentialAccessLevel, string> = {
    General: "bg-blue-100 text-blue-700 dark:bg-blue-900 dark:text-blue-300",
    Clinical: "bg-purple-100 text-purple-700 dark:bg-purple-900 dark:text-purple-300",
    Restricted: "bg-orange-100 text-orange-700 dark:bg-orange-900 dark:text-orange-300",
    Security: "bg-red-100 text-red-700 dark:bg-red-900 dark:text-red-300",
  };

  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${styles[level]}`}>
      {level}
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
