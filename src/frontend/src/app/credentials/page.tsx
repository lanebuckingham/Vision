"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { getCredentials, getCredentialSummary } from "@/lib/api/client";
import type {
  PagedList,
  CredentialListItemDto,
  CredentialSummaryDto,
  CredentialStatus,
  CredentialAccessLevel,
} from "@/lib/api/types";

const STATUS_OPTIONS: CredentialStatus[] = ["Active", "Expired", "Revoked"];
const ACCESS_LEVEL_OPTIONS: CredentialAccessLevel[] = ["General", "Clinical", "Restricted", "Security"];

export default function CredentialsPage() {
  const [data, setData] = useState<PagedList<CredentialListItemDto> | null>(null);
  const [summary, setSummary] = useState<CredentialSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [statusFilter, setStatusFilter] = useState("");
  const [accessLevelFilter, setAccessLevelFilter] = useState("");
  const [expiringSoonFilter, setExpiringSoonFilter] = useState(false);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

  useEffect(() => {
    getCredentialSummary()
      .then(setSummary)
      .catch(() => {}); // non-blocking
  }, []);

  useEffect(() => {
    getCredentials({
      status: statusFilter || undefined,
      accessLevel: accessLevelFilter || undefined,
      expiringSoon: expiringSoonFilter || undefined,
      search: search || undefined,
      page,
      pageSize: 25,
    })
      .then((result) => { setData(result); setError(null); })
      .catch((e) => setError(e instanceof Error ? e.message : "Failed to load credentials"))
      .finally(() => setLoading(false));
  }, [statusFilter, accessLevelFilter, expiringSoonFilter, search, page]);

  const handleExpiringSoon = () => {
    setExpiringSoonFilter(!expiringSoonFilter);
    setStatusFilter("");
    setPage(1);
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Credential Management</h1>
        <p className="text-sm text-gray-500 dark:text-gray-400">Physical access credentials and personnel</p>
      </div>

      {/* Summary cards */}
      {summary && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <SummaryCard
            label="Active"
            value={summary.activeCount}
            status="good"
            onClick={() => { setStatusFilter("Active"); setExpiringSoonFilter(false); setPage(1); }}
            active={statusFilter === "Active" && !expiringSoonFilter}
          />
          <SummaryCard
            label="Expiring Soon"
            value={summary.expiringSoonCount}
            status={summary.expiringSoonCount > 0 ? "warning" : "good"}
            onClick={handleExpiringSoon}
            active={expiringSoonFilter}
          />
          <SummaryCard
            label="Expired"
            value={summary.expiredCount}
            status={summary.expiredCount > 0 ? "warning" : "good"}
            onClick={() => { setStatusFilter("Expired"); setExpiringSoonFilter(false); setPage(1); }}
            active={statusFilter === "Expired"}
          />
          <SummaryCard
            label="Revoked"
            value={summary.revokedCount}
            status={summary.revokedCount > 0 ? "critical" : "good"}
            onClick={() => { setStatusFilter("Revoked"); setExpiringSoonFilter(false); setPage(1); }}
            active={statusFilter === "Revoked"}
          />
        </div>
      )}

      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <input
          type="search"
          placeholder="Search credentials, people..."
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1); }}
          className="rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
          aria-label="Search credentials"
        />
        <select
          value={statusFilter}
          onChange={(e) => { setStatusFilter(e.target.value); setExpiringSoonFilter(false); setPage(1); }}
          aria-label="Filter by status"
          className="rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
        >
          <option value="">All statuses</option>
          {STATUS_OPTIONS.map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
        <select
          value={accessLevelFilter}
          onChange={(e) => { setAccessLevelFilter(e.target.value); setPage(1); }}
          aria-label="Filter by access level"
          className="rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-white"
        >
          <option value="">All access levels</option>
          {ACCESS_LEVEL_OPTIONS.map((al) => (
            <option key={al} value={al}>{al}</option>
          ))}
        </select>
        {(statusFilter || accessLevelFilter || expiringSoonFilter || search) && (
          <button
            onClick={() => { setStatusFilter(""); setAccessLevelFilter(""); setExpiringSoonFilter(false); setSearch(""); setPage(1); }}
            className="rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-600 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-400 dark:hover:bg-gray-800"
          >
            Clear filters
          </button>
        )}
      </div>

      {/* Content */}
      {loading && !data && <CredentialListSkeleton />}

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-6 dark:border-red-900 dark:bg-red-950">
          <h2 className="text-lg font-semibold text-red-800 dark:text-red-200">Unable to load credentials</h2>
          <p className="mt-2 text-sm text-red-600 dark:text-red-400">{error}</p>
        </div>
      )}

      {!loading && !error && data && data.items.length === 0 && (
        <div className="rounded-lg border border-gray-200 p-8 text-center dark:border-gray-800">
          <p className="text-sm text-gray-500 dark:text-gray-400">No credentials found matching your filters.</p>
        </div>
      )}

      {data && data.items.length > 0 && (
        <>
          <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-800">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 dark:bg-gray-900">
                <tr>
                  <th className="px-4 py-3 text-left font-medium text-gray-600 dark:text-gray-400">Credential</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-600 dark:text-gray-400">Person</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-600 dark:text-gray-400">Access Level</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-600 dark:text-gray-400">Status</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-600 dark:text-gray-400">Expires</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 dark:divide-gray-800">
                {data.items.map((cred) => (
                  <tr key={cred.id} className="hover:bg-gray-50 dark:hover:bg-gray-900">
                    <td className="px-4 py-3">
                      <Link href={`/credentials/${cred.id}`} className="font-medium text-blue-600 hover:underline dark:text-blue-400">
                        {cred.credentialNumber}
                      </Link>
                    </td>
                    <td className="px-4 py-3">
                      <Link href={`/people/${cred.person.id}`} className="text-gray-900 hover:text-blue-600 dark:text-gray-100 dark:hover:text-blue-400">
                        {cred.person.displayName}
                      </Link>
                      <p className="text-xs text-gray-500 dark:text-gray-500">
                        {cred.person.department || cred.person.personType}
                      </p>
                    </td>
                    <td className="px-4 py-3">
                      <AccessLevelBadge level={cred.accessLevel} />
                    </td>
                    <td className="px-4 py-3">
                      <CredentialStatusBadge status={cred.status} isExpiringSoon={cred.isExpiringSoon} />
                    </td>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-300">
                      {new Date(cred.expiresAt).toLocaleDateString()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {data.totalCount > data.pageSize && (
            <div className="flex items-center justify-between">
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Page {data.page} of {Math.ceil(data.totalCount / data.pageSize)} ({data.totalCount} total)
              </p>
              <div className="flex gap-2">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className="rounded-lg border border-gray-300 px-3 py-1 text-sm disabled:opacity-50 dark:border-gray-700 dark:text-gray-300"
                >
                  Previous
                </button>
                <button
                  onClick={() => setPage((p) => p + 1)}
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

function SummaryCard({ label, value, status, onClick, active }: {
  label: string;
  value: number;
  status: "good" | "warning" | "critical";
  onClick: () => void;
  active: boolean;
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
    <button
      onClick={onClick}
      className={`rounded-lg border p-4 text-left transition-colors hover:bg-gray-50 dark:hover:bg-gray-800 ${borderColor} ${active ? "ring-2 ring-blue-500" : ""}`}
    >
      <p className="text-sm font-medium text-gray-600 dark:text-gray-400">{label}</p>
      <p className={`mt-1 text-2xl font-bold ${valueColor}`}>{value}</p>
    </button>
  );
}

function CredentialStatusBadge({ status, isExpiringSoon }: { status: CredentialStatus; isExpiringSoon: boolean }) {
  if (status === "Active" && isExpiringSoon) {
    return (
      <span className="inline-flex items-center rounded-full bg-yellow-100 px-2.5 py-0.5 text-xs font-medium text-yellow-700 dark:bg-yellow-900 dark:text-yellow-300">
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
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${styles[status]}`}>
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
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${styles[level]}`}>
      {level}
    </span>
  );
}

function CredentialListSkeleton() {
  return (
    <div className="space-y-2 animate-pulse">
      {[1, 2, 3, 4, 5].map((i) => (
        <div key={i} className="h-16 rounded-lg bg-gray-100 dark:bg-gray-800" />
      ))}
    </div>
  );
}
