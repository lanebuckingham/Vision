import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";

/**
 * AuthContext reads NEXT_PUBLIC_COGNITO_DOMAIN / NEXT_PUBLIC_COGNITO_CLIENT_ID at
 * module load time and holds session state in the module-level tokenStore, so
 * each test that needs a fresh Cognito "configured" instance must reset modules
 * and re-import both AuthContext and tokenStore together — otherwise the test's
 * tokenStore reference and AuthContext's internal tokenStore reference would be
 * two different module instances.
 */
async function loadAuthModules() {
  vi.resetModules();
  const tokenStoreModule = await import("./tokenStore");
  const authContextModule = await import("./AuthContext");
  return { tokenStoreModule, authContextModule };
}

function jwtWithPayload(payload: Record<string, unknown>): string {
  const header = btoa(JSON.stringify({ alg: "RS256", typ: "JWT" }));
  const body = btoa(JSON.stringify(payload));
  return `${header}.${body}.fake-signature`;
}

function TestConsumer({ useAuth }: { useAuth: typeof import("./AuthContext").useAuth }) {
  const { isAuthenticated, isLoading, user } = useAuth();
  return (
    <div>
      <div data-testid="isLoading">{String(isLoading)}</div>
      <div data-testid="isAuthenticated">{String(isAuthenticated)}</div>
      <div data-testid="user">{user ? user.sub : "none"}</div>
      <div data-testid="roles">{user ? user.roles.join(",") : ""}</div>
    </div>
  );
}

describe("AuthContext OAuth/PKCE callback handling", () => {
  beforeEach(() => {
    vi.stubEnv("NEXT_PUBLIC_COGNITO_DOMAIN", "https://vision-test.auth.us-east-1.amazoncognito.com");
    vi.stubEnv("NEXT_PUBLIC_COGNITO_CLIENT_ID", "test-client-id");
    sessionStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.restoreAllMocks();
    sessionStorage.clear();
    window.history.pushState({}, "", "/");
  });

  it("rejects the callback when the returned state does not match the stored state", async () => {
    sessionStorage.setItem("vision_oauth_state", "correct-state");
    sessionStorage.setItem("vision_pkce_verifier", "some-verifier");
    window.history.pushState({}, "", "/auth/callback?code=auth-code&state=wrong-state");

    const { tokenStoreModule, authContextModule } = await loadAuthModules();
    const { AuthProvider } = authContextModule;

    render(
      <AuthProvider>
        <TestConsumer useAuth={authContextModule.useAuth} />
      </AuthProvider>
    );

    await waitFor(() =>
      expect(screen.getByText(/Invalid OAuth state parameter/)).toBeInTheDocument()
    );

    expect(tokenStoreModule.getToken()).toBeNull();
    expect(sessionStorage.getItem("vision_auth")).toBeNull();
  });

  it("rejects the callback when no state was ever stored (missing state)", async () => {
    sessionStorage.setItem("vision_pkce_verifier", "some-verifier");
    window.history.pushState({}, "", "/auth/callback?code=auth-code&state=whatever");

    const { tokenStoreModule, authContextModule } = await loadAuthModules();
    const { AuthProvider } = authContextModule;

    render(
      <AuthProvider>
        <TestConsumer useAuth={authContextModule.useAuth} />
      </AuthProvider>
    );

    await waitFor(() =>
      expect(screen.getByText(/Invalid OAuth state parameter/)).toBeInTheDocument()
    );

    expect(tokenStoreModule.getToken()).toBeNull();
  });

  it("rejects the callback when the PKCE verifier is missing", async () => {
    sessionStorage.setItem("vision_oauth_state", "matching-state");
    // Deliberately no vision_pkce_verifier stored.
    window.history.pushState({}, "", "/auth/callback?code=auth-code&state=matching-state");

    const { tokenStoreModule, authContextModule } = await loadAuthModules();
    const { AuthProvider } = authContextModule;

    render(
      <AuthProvider>
        <TestConsumer useAuth={authContextModule.useAuth} />
      </AuthProvider>
    );

    await waitFor(() =>
      expect(screen.getByText(/Missing PKCE code verifier/)).toBeInTheDocument()
    );

    expect(tokenStoreModule.getToken()).toBeNull();
  });

  it("stores the access token and session after a successful mocked token exchange", async () => {
    sessionStorage.setItem("vision_oauth_state", "matching-state");
    sessionStorage.setItem("vision_pkce_verifier", "matching-verifier");
    window.history.pushState({}, "", "/auth/callback?code=auth-code&state=matching-state");

    const accessToken = jwtWithPayload({
      sub: "user-123",
      username: "jane.manager",
      "cognito:groups": ["SecurityManager"],
    });

    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify({ access_token: accessToken }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      })
    );

    const { tokenStoreModule, authContextModule } = await loadAuthModules();
    const { AuthProvider } = authContextModule;

    render(
      <AuthProvider>
        <TestConsumer useAuth={authContextModule.useAuth} />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId("isAuthenticated").textContent).toBe("true"));

    expect(screen.getByTestId("user").textContent).toBe("user-123");
    expect(screen.getByTestId("roles").textContent).toBe("SecurityManager");
    expect(tokenStoreModule.getToken()).toBe(accessToken);
    expect(sessionStorage.getItem("vision_auth")).not.toBeNull();

    // One-time-use OAuth artifacts must be consumed, not left around for replay.
    expect(sessionStorage.getItem("vision_oauth_state")).toBeNull();
    expect(sessionStorage.getItem("vision_pkce_verifier")).toBeNull();
  });

  it("clears stored authentication state on logout", async () => {
    // Seed an already-authenticated session the same way restoreSession() expects.
    const accessToken = jwtWithPayload({ sub: "user-456", "cognito:groups": ["Technician"] });
    sessionStorage.setItem(
      "vision_auth",
      JSON.stringify({
        user: { sub: "user-456", name: "user-456", email: "", roles: ["Technician"] },
        accessToken,
      })
    );
    window.history.pushState({}, "", "/dashboard");

    const { tokenStoreModule, authContextModule } = await loadAuthModules();
    const { AuthProvider, useAuth } = authContextModule;

    // Logout redirects via window.location.href; stub assignment so jsdom doesn't
    // error attempting real navigation.
    const originalHref = window.location.href;
    Object.defineProperty(window, "location", {
      value: { ...window.location, href: originalHref, assign: vi.fn() },
      writable: true,
    });

    function LogoutHarness() {
      const { isAuthenticated, isLoading, logout } = useAuth();
      return (
        <div>
          <div data-testid="isLoading">{String(isLoading)}</div>
          <div data-testid="isAuthenticated">{String(isAuthenticated)}</div>
          <button onClick={logout}>Sign Out</button>
        </div>
      );
    }

    render(
      <AuthProvider>
        <LogoutHarness />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId("isLoading").textContent).toBe("false"));
    expect(screen.getByTestId("isAuthenticated").textContent).toBe("true");

    screen.getByText("Sign Out").click();

    expect(tokenStoreModule.getToken()).toBeNull();
    expect(sessionStorage.getItem("vision_auth")).toBeNull();
  });
});
