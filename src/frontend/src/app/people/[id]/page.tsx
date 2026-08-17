"use client";

import { useEffect, useState, useMemo } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { getPersonById, issueCredential, revokeCredential } from "@/lib/api/client";
import type {
  PersonDetailDto,
  PersonCredentialDto,
  CredentialStatus,
  CredentialAccessLevel,
  IssueCredentialRequest,
} from "@/lib/api/types";
import { ApiError } from "@/lib/api/client";

const ACCESS_LEVEL_OPTIONS: CredentialAccessLevel[] = ["General", "Clinical", "Restricted", "Security"];

export default function PersonDetailPage() {
  const params = useParams<{ id: string }>();
  const [person, setPerson] = useState<PersonDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Issue credential state
  const [showIssueForm, setShowIssueForm] = useState(false);
  const [issueNumber, setIssueNumber] = useState("");
  const [issueAccessLevel, setIssueAccessLevel] = useState<CredentialAccessLevel>("General");
  const [issueExpiresAt, setIssueExpiresAt] = useState("");
  const [issueLoading, setIssueLoading] = useState(false);
  const [issueError, setIssueError] = useState<string | null>(null);

  const minExpirationDate = useMemo(() => {
    const d = new Date();
    d.setDate(d.getDate() + 1);
    return d.toISOString().split("T")[0];
  }, []);

  // Revoke state
  const [revokeCredentialId, setRevokeCredentialId] = useState<string | null>(null);
  const [revokeReason, setRevokeReason] = useState("");
  const [revokeLoading, setRevokeLoading] = useState(false);
  const [revokeError, setRevokeError] = useState<string | null>(null);

  const loadPerson = () => {
    if (!params.id) return;
    getPersonById(params.id)
      .then(setPerson)
      .catch((e) => setError(e instanceof Error ? e.message : "Failed to load person"))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadPerson();
  }, [params.id]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleIssue = async () => {
    if (!params.id || !issueNumber.trim() || !issueExpiresAt) return;
    setIssueLoading(true);
    setIssueError(null);
    try {
      const data: IssueCredentialRequest = {
        credentialNumber: issueNumber.trim(),
        accessLevel: issueAccessLevel,
        expiresAt: new Date(issueExpiresAt).toISOString(),
      };
      await issueCredential(params.id, data);
      setShowIssueForm(false);
      setIssueNumber("");
      setIssueExpiresAt("");
      setIssueAccessLevel("General");
      // Reload person to show new credential
      setLoading(true);
      loadPerson();
    } catch (e) {
      if (e instanceof ApiError) {
        setIssueError(e.message);
      } else {
        setIssueError(e instanceof Error ? e.message : "Failed to issue credential");
      }
    } finally {
      setIssueLoading(false);
    }
  };

  const handleRevoke = async () => {
    if (!revokeCredentialId || !revokeReason.trim()) return;
    setRevokeLoading(true);
    setRevokeError(null);
    try {
      await revokeCredential(revokeCredentialId, { reason: revokeReason.trim() });
      setRevokeCredentialId(null);
      setRevokeReason("");
      // Reload
      setLoading(true);
      loadPerson();
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

  if (!person) return null;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <Link href="/credentials" className="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300">
            ← Credential Management
          </Link>
          <h1 className="mt-1 text-2xl font-bold text-gray-900 dark:text-white">{person.displayName}</h1>
          <div className="mt-1 flex items-center gap-2">
            <PersonTypeBadge type={person.personType} />
            {!person.isActive && (
              <span className="inline-flex items-center rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium text-gray-700 dark:bg-gray-800 dark:text-gray-300">
                Inactive
              </span>
            )}
          </div>
        </div>
        {person.isActive && (
          <button
            onClick={() => setShowIssueForm(true)}
            className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            Issue Credential
          </button>
        )}
      </div>

      {/* Person details */}
      <div className="rounded-lg border border-gray-200 p-4 dark:border-gray-800">
        <h2 className="mb-3 text-sm font-semibold uppercase text-gray-500 dark:text-gray-400">Personnel Details</h2>
        <dl className="grid gap-2 text-sm sm:grid-cols-2">
          {person.employeeNumber && <DetailRow label="Personnel ID" value={person.employeeNumber} />}
          <DetailRow label="Type" value={person.personType} />
          {person.department && <DetailRow label="Department" value={person.department} />}
          {person.jobTitle && <DetailRow label="Job Title" value={person.jobTitle} />}
          {person.email && <DetailRow label="Email" value={person.email} />}
          <DetailRow label="Status" value={person.isActive ? "Active" : "Inactive"} />
        </dl>
      </div>

      {/* Issue credential form */}
      {showIssueForm && (
        <div className="rounded-lg border border-blue-200 bg-blue-50 p-4 dark:border-blue-900 dark:bg-blue-950">
          <h3 className="text-sm font-semibold text-blue-800 dark:text-blue-200">Issue New Credential</h3>
          {issueError && (
            <p className="mt-2 text-sm text-red-600 dark:text-red-400">{issueError}</p>
          )}
          <div className="mt-3 grid gap-3 sm:grid-cols-3">
            <div>
              <label htmlFor="issue-number" className="block text-xs font-medium text-gray-700 dark:text-gray-300">
                Credential Number
              </label>
              <input
                id="issue-number"
                type="text"
                value={issueNumber}
                onChange={(e) => setIssueNumber(e.target.value)}
                maxLength={50}
                placeholder="NMC-00020"
                className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
              />
            </div>
            <div>
              <label htmlFor="issue-access-level" className="block text-xs font-medium text-gray-700 dark:text-gray-300">
                Access Level
              </label>
              <select
                id="issue-access-level"
                value={issueAccessLevel}
                onChange={(e) => setIssueAccessLevel(e.target.value as CredentialAccessLevel)}
                className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
              >
                {ACCESS_LEVEL_OPTIONS.map((al) => (
                  <option key={al} value={al}>{al}</option>
                ))}
              </select>
            </div>
            <div>
              <label htmlFor="issue-expires" className="block text-xs font-medium text-gray-700 dark:text-gray-300">
                Expiration Date
              </label>
              <input
                id="issue-expires"
                type="date"
                value={issueExpiresAt}
                onChange={(e) => setIssueExpiresAt(e.target.value)}
                min={minExpirationDate}
                className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
              />
            </div>
          </div>
          <div className="mt-4 flex gap-2">
            <button
              onClick={handleIssue}
              disabled={!issueNumber.trim() || !issueExpiresAt || issueLoading}
              className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {issueLoading ? "Issuing..." : "Issue Credential"}
            </button>
            <button
              onClick={() => { setShowIssueForm(false); setIssueError(null); }}
              className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300"
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      {/* Revoke confirmation dialog */}
      {revokeCredentialId && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 dark:border-red-900 dark:bg-red-950">
          <h3 className="text-sm font-semibold text-red-800 dark:text-red-200">
            Revoke Credential
          </h3>
          <p className="mt-1 text-sm text-red-700 dark:text-red-300">
            This action is permanent. The credential for {person.displayName} will be immediately revoked.
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
              onClick={() => { setRevokeCredentialId(null); setRevokeReason(""); setRevokeError(null); }}
              className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300"
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      {/* Credentials list */}
      <section>
        <h2 className="mb-3 text-lg font-semibold text-gray-900 dark:text-white">Credentials</h2>
        {person.credentials.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No credentials on record.</p>
        ) : (
          <div className="divide-y divide-gray-100 rounded-lg border border-gray-200 dark:divide-gray-800 dark:border-gray-800">
            {person.credentials.map((cred) => (
              <CredentialRow
                key={cred.id}
                credential={cred}
                onRevoke={() => setRevokeCredentialId(cred.id)}
              />
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function CredentialRow({ credential, onRevoke }: { credential: PersonCredentialDto; onRevoke: () => void }) {
  const canRevoke = credential.status !== "Revoked";

  return (
    <div className="flex items-center justify-between px-4 py-3">
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <Link
            href={`/credentials/${credential.id}`}
            className="font-medium text-blue-600 hover:underline dark:text-blue-400"
          >
            {credential.credentialNumber}
          </Link>
          <CredentialStatusBadge status={credential.status} isExpiringSoon={credential.isExpiringSoon} />
          <AccessLevelBadge level={credential.accessLevel} />
        </div>
        <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">
          Issued {new Date(credential.issuedAt).toLocaleDateString()} · Expires {new Date(credential.expiresAt).toLocaleDateString()}
          {credential.revokedAt && ` · Revoked ${new Date(credential.revokedAt).toLocaleDateString()}`}
        </p>
        {credential.revocationReason && (
          <p className="mt-0.5 text-xs text-red-600 dark:text-red-400">
            Reason: {credential.revocationReason}
          </p>
        )}
      </div>
      {canRevoke && (
        <button
          onClick={onRevoke}
          className="rounded-lg border border-red-300 px-3 py-1 text-xs font-medium text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
        >
          Revoke
        </button>
      )}
    </div>
  );
}

function CredentialStatusBadge({ status, isExpiringSoon }: { status: CredentialStatus; isExpiringSoon: boolean }) {
  if (status === "Active" && isExpiringSoon) {
    return (
      <span className="inline-flex items-center rounded-full bg-yellow-100 px-2 py-0.5 text-xs font-medium text-yellow-700 dark:bg-yellow-900 dark:text-yellow-300">
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
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${styles[status]}`}>
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
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${styles[level]}`}>
      {level}
    </span>
  );
}

function PersonTypeBadge({ type }: { type: string }) {
  const styles = type === "Employee"
    ? "bg-blue-100 text-blue-700 dark:bg-blue-900 dark:text-blue-300"
    : "bg-purple-100 text-purple-700 dark:bg-purple-900 dark:text-purple-300";

  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${styles}`}>
      {type}
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
