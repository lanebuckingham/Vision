import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";

/**
 * AppShell's navigation visibility is the frontend's clearest expression of
 * Vision's role model: SecurityManager, Technician, and CredentialAdministrator
 * each see a different slice of the app. These are UX regression smoke tests —
 * backend authorization remains authoritative.
 */

vi.mock("next/navigation", () => ({
  usePathname: () => "/dashboard",
}));

vi.mock("next/link", () => ({
  default: ({ href, children, ...rest }: { href: string; children: React.ReactNode }) => (
    <a href={href} {...rest}>{children}</a>
  ),
}));

const mockUseAuth = vi.fn();
vi.mock("@/lib/auth/AuthContext", () => ({
  useAuth: () => mockUseAuth(),
}));

async function renderAppShellAs(roles: string[]) {
  mockUseAuth.mockReturnValue({
    user: { sub: "test-user", name: "Test User", email: "", roles },
    isAuthenticated: true,
    isLoading: false,
    login: vi.fn(),
    logout: vi.fn(),
    hasRole: (role: string) => roles.includes(role),
    hasAnyRole: (...check: string[]) => check.some((r) => roles.includes(r)),
  });

  const { AppShell } = await import("./AppShell");
  render(<AppShell><div>content</div></AppShell>);
}

describe("AppShell role-aware navigation", () => {
  it("shows SecurityManager the Dashboard, Assets, Incidents, Work Orders, and Credentials nav items", async () => {
    await renderAppShellAs(["SecurityManager"]);

    expect(screen.getAllByRole("link", { name: /Dashboard/ }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole("link", { name: /Assets/ }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole("link", { name: /Incidents/ }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole("link", { name: /Work Orders/ }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole("link", { name: /Credentials/ }).length).toBeGreaterThan(0);
  });

  it("shows Technician only Work Orders and hides SecurityManager-only sections", async () => {
    await renderAppShellAs(["Technician"]);

    expect(screen.getAllByRole("link", { name: /Work Orders/ }).length).toBeGreaterThan(0);
    expect(screen.queryByRole("link", { name: /Dashboard/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Assets/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Incidents/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Credentials/ })).not.toBeInTheDocument();
  });

  it("shows CredentialAdministrator only Credentials and hides operational sections", async () => {
    await renderAppShellAs(["CredentialAdministrator"]);

    expect(screen.getAllByRole("link", { name: /Credentials/ }).length).toBeGreaterThan(0);
    expect(screen.queryByRole("link", { name: /Dashboard/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Assets/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Incidents/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Work Orders/ })).not.toBeInTheDocument();
  });

  it("shows the sign-in prompt and no navigation when unauthenticated", async () => {
    mockUseAuth.mockReturnValue({
      user: null,
      isAuthenticated: false,
      isLoading: false,
      login: vi.fn(),
      logout: vi.fn(),
      hasRole: () => false,
      hasAnyRole: () => false,
    });

    const { AppShell } = await import("./AppShell");
    render(<AppShell><div>content</div></AppShell>);

    expect(screen.getByRole("button", { name: /Sign In/ })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Dashboard/ })).not.toBeInTheDocument();
  });
});
