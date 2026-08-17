"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth, type UserRole } from "@/lib/auth/AuthContext";

interface NavItem {
  href: string;
  label: string;
  icon: string;
  /** If set, item is visible only to users with at least one of these roles */
  roles?: UserRole[];
}

const navItems: NavItem[] = [
  { href: "/dashboard", label: "Dashboard", icon: "📊", roles: ["SecurityManager"] },
  { href: "/assets", label: "Assets", icon: "🔒", roles: ["SecurityManager"] },
  { href: "/incidents", label: "Incidents", icon: "⚠️", roles: ["SecurityManager"] },
  { href: "/work-orders", label: "Work Orders", icon: "🔧", roles: ["SecurityManager", "Technician"] },
  { href: "/credentials", label: "Credentials", icon: "🪪", roles: ["SecurityManager", "CredentialAdministrator"] },
];

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const { user, isAuthenticated, isLoading, login, logout, hasAnyRole } = useAuth();

  const visibleNavItems = navItems.filter(
    (item) => !item.roles || hasAnyRole(...item.roles)
  );

  if (isLoading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="text-center">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-gray-300 border-t-blue-600 mx-auto" />
          <p className="mt-3 text-sm text-gray-500 dark:text-gray-400">Loading...</p>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="text-center space-y-4">
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Vision</h1>
          <p className="text-sm text-gray-500 dark:text-gray-400">Physical Security Operations</p>
          <button
            onClick={login}
            className="rounded-lg bg-blue-600 px-6 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            Sign In
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-full min-h-screen">
      {/* Sidebar */}
      <aside className="hidden md:flex md:w-64 md:flex-col md:border-r md:border-gray-200 md:bg-gray-50 dark:md:border-gray-800 dark:md:bg-gray-900">
        <div className="flex h-16 items-center border-b border-gray-200 px-6 dark:border-gray-800">
          <Link href="/dashboard" className="text-xl font-bold text-gray-900 dark:text-white">
            Vision
          </Link>
        </div>
        <nav className="flex-1 px-4 py-4 space-y-1">
          {visibleNavItems.map((item) => {
            const isActive = pathname.startsWith(item.href);
            return (
              <Link
                key={item.href}
                href={item.href}
                className={`flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
                  isActive
                    ? "bg-gray-200 text-gray-900 dark:bg-gray-800 dark:text-white"
                    : "text-gray-600 hover:bg-gray-100 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-gray-800 dark:hover:text-white"
                }`}
                aria-current={isActive ? "page" : undefined}
              >
                <span aria-hidden="true">{item.icon}</span>
                {item.label}
              </Link>
            );
          })}
        </nav>
        <div className="border-t border-gray-200 px-4 py-4 dark:border-gray-800">
          {user && (
            <div className="mb-3 px-2">
              <p className="text-sm font-medium text-gray-900 dark:text-white truncate">{user.name}</p>
              <p className="text-xs text-gray-500 dark:text-gray-500 truncate">{user.roles.join(", ")}</p>
            </div>
          )}
          <button
            onClick={logout}
            className="w-full rounded-lg px-3 py-2 text-left text-xs text-gray-500 hover:bg-gray-100 hover:text-gray-700 dark:text-gray-500 dark:hover:bg-gray-800 dark:hover:text-gray-300"
          >
            Sign Out
          </button>
        </div>
      </aside>

      {/* Mobile header */}
      <div className="flex flex-1 flex-col">
        <header className="flex h-16 items-center justify-between border-b border-gray-200 px-4 md:hidden dark:border-gray-800">
          <Link href="/dashboard" className="text-lg font-bold text-gray-900 dark:text-white">
            Vision
          </Link>
          <nav className="flex gap-4">
            {visibleNavItems.map((item) => {
              const isActive = pathname.startsWith(item.href);
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={`text-sm font-medium ${
                    isActive
                      ? "text-gray-900 dark:text-white"
                      : "text-gray-500 dark:text-gray-400"
                  }`}
                  aria-current={isActive ? "page" : undefined}
                >
                  {item.label}
                </Link>
              );
            })}
          </nav>
        </header>

        {/* Page content */}
        <main className="flex-1 overflow-y-auto p-6">
          {children}
        </main>
      </div>
    </div>
  );
}
