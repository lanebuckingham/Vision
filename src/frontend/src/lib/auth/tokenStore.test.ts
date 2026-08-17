import { describe, it, expect, vi, beforeEach } from "vitest";
import { setToken, getToken, registerSessionExpiredHandler, notifySessionExpired } from "./tokenStore";

describe("tokenStore", () => {
  beforeEach(() => {
    setToken(null);
  });

  it("returns null when no token has been set", () => {
    expect(getToken()).toBeNull();
  });

  it("returns the token that was set", () => {
    setToken("abc123");
    expect(getToken()).toBe("abc123");
  });

  it("clears the token when set to null", () => {
    setToken("abc123");
    setToken(null);
    expect(getToken()).toBeNull();
  });

  it("notifies the registered session-expired handler", () => {
    const handler = vi.fn();
    registerSessionExpiredHandler(handler);

    notifySessionExpired();

    expect(handler).toHaveBeenCalledTimes(1);
  });

  it("does not call a handler after it has been unregistered", () => {
    const handler = vi.fn();
    const unregister = registerSessionExpiredHandler(handler);
    unregister();

    notifySessionExpired();

    expect(handler).not.toHaveBeenCalled();
  });

  it("only the most recently registered handler is notified", () => {
    const first = vi.fn();
    const second = vi.fn();
    registerSessionExpiredHandler(first);
    registerSessionExpiredHandler(second);

    notifySessionExpired();

    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledTimes(1);
  });
});
