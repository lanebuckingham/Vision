import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { setToken, registerSessionExpiredHandler } from "@/lib/auth/tokenStore";
import { getDashboard, ApiError } from "./client";

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

describe("api client", () => {
  beforeEach(() => {
    setToken(null);
    vi.restoreAllMocks();
  });

  afterEach(() => {
    setToken(null);
  });

  it("attaches a Bearer token header when a token is present", async () => {
    setToken("test-access-token");
    const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(jsonResponse({ ok: true }));

    await getDashboard();

    expect(fetchSpy).toHaveBeenCalledTimes(1);
    const [, options] = fetchSpy.mock.calls[0];
    const headers = options?.headers as Record<string, string>;
    expect(headers.Authorization).toBe("Bearer test-access-token");
  });

  it("does not fabricate an Authorization header when no token is present", async () => {
    const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(jsonResponse({ ok: true }));

    await getDashboard();

    const [, options] = fetchSpy.mock.calls[0];
    const headers = options?.headers as Record<string, string>;
    expect(headers.Authorization).toBeUndefined();
  });

  it("notifies the session-expired handler when a request returns 401", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      jsonResponse({ title: "Unauthorized" }, 401)
    );

    const handler = vi.fn();
    const unregister = registerSessionExpiredHandler(handler);

    await expect(getDashboard()).rejects.toBeInstanceOf(ApiError);
    expect(handler).toHaveBeenCalledTimes(1);

    unregister();
  });

  it("does not notify the session-expired handler on a successful response", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(jsonResponse({ ok: true }));

    const handler = vi.fn();
    const unregister = registerSessionExpiredHandler(handler);

    await getDashboard();

    expect(handler).not.toHaveBeenCalled();
    unregister();
  });

  it("propagates non-2xx responses as ApiError with status and server-provided detail", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      jsonResponse({ title: "Bad Request", detail: "Invalid severity value." }, 400)
    );

    const error = await getDashboard().catch((e) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(400);
    expect((error as ApiError).message).toBe("Invalid severity value.");
  });

  it("falls back to the title when no detail is present on an error response", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      jsonResponse({ title: "Forbidden" }, 403)
    );

    const error = await getDashboard().catch((e) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(403);
    expect((error as ApiError).message).toBe("Forbidden");
  });

  it("falls back to a generic message when the error response has no JSON body", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response("", { status: 500 })
    );

    const error = await getDashboard().catch((e) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(500);
    expect((error as ApiError).message).toBe("API error: 500");
  });
});
