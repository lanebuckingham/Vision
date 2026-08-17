/**
 * Module-level access token store.
 * Set by AuthProvider, read by API client to attach Bearer header.
 */
let currentToken: string | null = null;

export function setToken(token: string | null) {
  currentToken = token;
}

export function getToken(): string | null {
  return currentToken;
}

/**
 * Session-expiry notification.
 * AuthProvider registers a handler; the API client invokes it when a request
 * comes back 401 so the stale session is cleared and the sign-in prompt returns.
 */
type SessionExpiredHandler = () => void;

let sessionExpiredHandler: SessionExpiredHandler | null = null;

export function registerSessionExpiredHandler(handler: SessionExpiredHandler): () => void {
  sessionExpiredHandler = handler;
  return () => {
    if (sessionExpiredHandler === handler) sessionExpiredHandler = null;
  };
}

export function notifySessionExpired() {
  sessionExpiredHandler?.();
}
