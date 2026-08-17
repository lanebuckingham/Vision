"use client";

import { createContext, useContext, useEffect, useState, useCallback, useMemo, type ReactNode } from "react";
import { setToken, registerSessionExpiredHandler } from "./tokenStore";

export type UserRole = "SecurityManager" | "Technician" | "CredentialAdministrator";

export interface AuthUser {
  sub: string;
  name: string;
  email: string;
  roles: UserRole[];
}

interface AuthContextValue {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  accessToken: string | null;
  login: () => void;
  logout: () => void;
  hasRole: (role: UserRole) => boolean;
  hasAnyRole: (...roles: UserRole[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

const COGNITO_DOMAIN = process.env.NEXT_PUBLIC_COGNITO_DOMAIN;
const COGNITO_CLIENT_ID = process.env.NEXT_PUBLIC_COGNITO_CLIENT_ID;
const COGNITO_REDIRECT_URI = process.env.NEXT_PUBLIC_COGNITO_REDIRECT_URI || (typeof window !== "undefined" ? window.location.origin + "/auth/callback" : "");
const COGNITO_LOGOUT_URI = process.env.NEXT_PUBLIC_COGNITO_LOGOUT_URI || (typeof window !== "undefined" ? window.location.origin : "");

const isCognitoConfigured = !!(COGNITO_DOMAIN && COGNITO_CLIENT_ID);

// --- PKCE helpers ---

function generateRandomString(length: number): string {
  const array = new Uint8Array(length);
  crypto.getRandomValues(array);
  return Array.from(array, (b) => b.toString(16).padStart(2, "0")).join("");
}

async function generateCodeChallenge(verifier: string): Promise<string> {
  const encoder = new TextEncoder();
  const data = encoder.encode(verifier);
  const digest = await crypto.subtle.digest("SHA-256", data);
  return btoa(String.fromCharCode(...new Uint8Array(digest)))
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/, "");
}

// --- Auth Provider ---

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [accessToken, setAccessToken] = useState<string | null>(null);
  // Without Cognito configuration there is nothing to load: render the sign-in prompt immediately.
  const [isLoading, setIsLoading] = useState(isCognitoConfigured);
  const [authError, setAuthError] = useState<string | null>(null);

  const handleCallback = useCallback(async (code: string, returnedState: string | null) => {
    // Yield so OAuth validation/setState is not synchronous with the mounting effect.
    await Promise.resolve();
    try {
      // Validate state
      const storedState = sessionStorage.getItem("vision_oauth_state");
      if (!storedState || storedState !== returnedState) {
        throw new Error("Invalid OAuth state parameter. Possible CSRF.");
      }
      sessionStorage.removeItem("vision_oauth_state");

      // Retrieve PKCE verifier
      const codeVerifier = sessionStorage.getItem("vision_pkce_verifier");
      if (!codeVerifier) {
        throw new Error("Missing PKCE code verifier.");
      }
      sessionStorage.removeItem("vision_pkce_verifier");

      // Exchange code for tokens
      const tokenUrl = `${COGNITO_DOMAIN}/oauth2/token`;
      const body = new URLSearchParams({
        grant_type: "authorization_code",
        client_id: COGNITO_CLIENT_ID!,
        code,
        redirect_uri: COGNITO_REDIRECT_URI,
        code_verifier: codeVerifier,
      });

      const res = await fetch(tokenUrl, {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: body.toString(),
      });

      if (!res.ok) throw new Error("Token exchange failed");

      const tokens = await res.json();
      const payload = parseJwt(tokens.access_token);

      const authUser: AuthUser = {
        sub: payload.sub as string,
        name: (payload.username || payload.sub || "User") as string,
        email: "",
        roles: ((payload["cognito:groups"] as string[]) || []) as UserRole[],
      };

      setUser(authUser);
      setAccessToken(tokens.access_token);
      setToken(tokens.access_token);
      sessionStorage.setItem("vision_auth", JSON.stringify({
        user: authUser,
        accessToken: tokens.access_token,
      }));
      setAuthError(null);
    } catch (e) {
      setAuthError(e instanceof Error ? e.message : "Authentication failed");
      console.error("Authentication error:", e);
    } finally {
      setIsLoading(false);
    }
  }, []);

  const restoreSession = useCallback(() => {
    const stored = sessionStorage.getItem("vision_auth");
    if (stored) {
      try {
        const parsed = JSON.parse(stored);
        setUser(parsed.user);
        setAccessToken(parsed.accessToken);
        setToken(parsed.accessToken);
      } catch { /* ignore corrupt data */ }
    }
  }, []);

  useEffect(() => {
    // Cognito is required. Without it the provider stays unauthenticated.
    if (!isCognitoConfigured) return;

    const params = new URLSearchParams(window.location.search);
    const code = params.get("code");
    const returnedState = params.get("state");

    let cancelled = false;

    const run = async () => {
      if (code) {
        window.history.replaceState({}, "", window.location.pathname);
        await handleCallback(code, returnedState);
        return;
      }

      await Promise.resolve();
      if (cancelled) return;
      restoreSession();
      setIsLoading(false);
    };

    void run();
    return () => {
      cancelled = true;
    };
  }, [handleCallback, restoreSession]);

  const login = useCallback(async () => {
    if (!isCognitoConfigured) return;

    // Generate PKCE values
    const codeVerifier = generateRandomString(64);
    const codeChallenge = await generateCodeChallenge(codeVerifier);
    sessionStorage.setItem("vision_pkce_verifier", codeVerifier);

    // Generate state for CSRF protection
    const state = generateRandomString(32);
    sessionStorage.setItem("vision_oauth_state", state);

    const authUrl = new URL(`${COGNITO_DOMAIN}/oauth2/authorize`);
    authUrl.searchParams.set("client_id", COGNITO_CLIENT_ID!);
    authUrl.searchParams.set("response_type", "code");
    authUrl.searchParams.set("scope", "openid");
    authUrl.searchParams.set("redirect_uri", COGNITO_REDIRECT_URI);
    authUrl.searchParams.set("state", state);
    authUrl.searchParams.set("code_challenge", codeChallenge);
    authUrl.searchParams.set("code_challenge_method", "S256");

    window.location.href = authUrl.toString();
  }, []);

  const logout = useCallback(() => {
    setUser(null);
    setAccessToken(null);
    setToken(null);
    sessionStorage.removeItem("vision_auth");
    sessionStorage.removeItem("vision_oauth_state");
    sessionStorage.removeItem("vision_pkce_verifier");

    if (isCognitoConfigured) {
      const logoutUrl = new URL(`${COGNITO_DOMAIN}/logout`);
      logoutUrl.searchParams.set("client_id", COGNITO_CLIENT_ID!);
      logoutUrl.searchParams.set("logout_uri", COGNITO_LOGOUT_URI);
      window.location.href = logoutUrl.toString();
    }
  }, []);

  /** Called by the API client when a 401 is received — clears stale session */
  const handleSessionExpired = useCallback(() => {
    setUser(null);
    setAccessToken(null);
    setToken(null);
    sessionStorage.removeItem("vision_auth");
  }, []);

  const hasRole = useCallback((role: UserRole) => user?.roles.includes(role) ?? false, [user]);
  const hasAnyRole = useCallback((...roles: UserRole[]) => roles.some((r) => user?.roles.includes(r)) ?? false, [user]);

  const value = useMemo(() => ({
    user,
    isAuthenticated: !!user,
    isLoading,
    accessToken,
    login,
    logout,
    hasRole,
    hasAnyRole,
  }), [user, isLoading, accessToken, login, logout, hasRole, hasAnyRole]);

  // Let the API client clear the session when a request returns 401.
  useEffect(() => registerSessionExpiredHandler(handleSessionExpired), [handleSessionExpired]);

  return (
    <AuthContext.Provider value={value}>
      {authError && !user ? (
        <div className="flex h-screen items-center justify-center">
          <div className="text-center space-y-4">
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Vision</h1>
            <p className="text-sm text-red-600 dark:text-red-400">{authError}</p>
            <button onClick={login} className="rounded-lg bg-blue-600 px-6 py-2 text-sm font-medium text-white hover:bg-blue-700">
              Try Again
            </button>
          </div>
        </div>
      ) : (
        children
      )}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used within AuthProvider");
  return context;
}

function parseJwt(token: string): Record<string, unknown> {
  const base64Url = token.split(".")[1];
  const base64 = base64Url.replace(/-/g, "+").replace(/_/g, "/");
  const jsonPayload = decodeURIComponent(
    atob(base64)
      .split("")
      .map((c) => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2))
      .join("")
  );
  return JSON.parse(jsonPayload);
}
