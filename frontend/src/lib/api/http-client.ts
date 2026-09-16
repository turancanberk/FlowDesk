import {
  clearAccessToken,
  getAccessToken,
  isAccessTokenStale,
  setAccessToken,
} from "./access-token-store";
import { ApiError, toApiError } from "./api-error";

/*
  The single HTTP client every authenticated request goes through.

  It owns three things that must not be duplicated across features:
    - attaching the bearer token
    - keeping the refresh cookie flowing on auth calls
    - recovering from an expired access token exactly once per request
*/

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

type RequestOptions = {
  method?: "GET" | "POST" | "PATCH" | "DELETE";
  body?: unknown;
  signal?: AbortSignal;
  /** Set for the auth endpoints themselves, which must not trigger a refresh. */
  skipAuthRefresh?: boolean;
};

type SessionResponse = {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: { id: string; email: string; displayName: string };
};

/*
  Single-flight refresh.

  Several queries can discover an expired token in the same tick. Without this,
  each would start its own refresh; because refresh tokens are single use, the
  first would succeed and the rest would look like replays — and the server
  would correctly revoke the whole family, signing the user out for doing
  nothing wrong.

  So the first caller starts the refresh and every other caller awaits the same
  promise.
*/
let inFlightRefresh: Promise<boolean> | null = null;

export async function refreshSession(): Promise<boolean> {
  inFlightRefresh ??= performRefresh().finally(() => {
    inFlightRefresh = null;
  });

  return inFlightRefresh;
}

async function performRefresh(): Promise<boolean> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/auth/refresh`, {
      method: "POST",
      // The refresh token rides in an HttpOnly cookie, so the request has to
      // carry credentials; script never sees the value.
      credentials: "include",
      headers: { "X-FlowDesk-Client": "web" },
    });

    if (!response.ok) {
      clearAccessToken();
      return false;
    }

    const session = (await response.json()) as SessionResponse;
    setAccessToken(session.accessToken, session.accessTokenExpiresAt);

    return true;
  } catch {
    clearAccessToken();
    return false;
  }
}

/**
 * Performs an authenticated request, refreshing the access token once if the
 * server rejects it.
 */
export async function apiFetch<TResponse>(
  path: string,
  options: RequestOptions = {},
): Promise<TResponse> {
  const { skipAuthRefresh = false } = options;

  // Refresh proactively when the token is known to be stale. Waiting for the
  // 401 would work, but it spends a round trip to learn something we already
  // know.
  if (!skipAuthRefresh && isAccessTokenStale()) {
    await refreshSession();
  }

  let response = await send(path, options);

  if (response.status === 401 && !skipAuthRefresh) {
    const refreshed = await refreshSession();

    if (refreshed) {
      response = await send(path, options);
    }
  }

  if (!response.ok) {
    const error = await toApiError(response);

    if (error.isUnauthorized) {
      clearAccessToken();
    }

    throw error;
  }

  if (response.status === 204) {
    return undefined as TResponse;
  }

  return (await response.json()) as TResponse;
}

async function send(path: string, options: RequestOptions): Promise<Response> {
  const headers: Record<string, string> = {
    // Forces a CORS preflight on cross-site requests, which blocks
    // form-based CSRF against the cookie-bearing auth endpoints
    // (docs/SECURITY.md).
    "X-FlowDesk-Client": "web",
  };

  const token = getAccessToken();

  if (token !== null) {
    headers.Authorization = `Bearer ${token}`;
  }

  let body: string | undefined;

  if (options.body !== undefined) {
    headers["Content-Type"] = "application/json";
    body = JSON.stringify(options.body);
  }

  return fetch(`${API_BASE_URL}${path}`, {
    method: options.method ?? "GET",
    headers,
    body,
    credentials: "include",
    signal: options.signal,
  });
}

export { ApiError, API_BASE_URL };
