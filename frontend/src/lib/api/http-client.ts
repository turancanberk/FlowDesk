import {
  clearAccessToken,
  getAccessToken,
  isAccessTokenStale,
  setAccessToken,
} from "./access-token-store";
import { ApiError, toApiError } from "./api-error";
import { API_BASE_URL } from "./api-origin";

/*
  The single HTTP client every authenticated request goes through.

  It owns three things that must not be duplicated across features:
    - attaching the bearer token
    - keeping the refresh cookie flowing on auth calls
    - recovering from an expired access token exactly once per request
*/

type RequestOptions = {
  method?: "GET" | "POST" | "PATCH" | "DELETE";
  body?: unknown;
  /**
   * A multipart body, for file uploads.
   *
   * Sent instead of {@link body}, and deliberately without a `Content-Type`
   * header: the browser has to set one itself because it alone knows the
   * multipart boundary it generated. Setting it by hand produces a body the
   * server cannot parse.
   */
  formData?: FormData;
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
  inFlightRefresh ??= performRefreshAcrossTabs().finally(() => {
    inFlightRefresh = null;
  });

  return inFlightRefresh;
}

/*
  Single-flight across tabs, too.

  The in-memory promise above covers one tab. Every tab of the site shares one
  refresh cookie, though, and two tabs waking together (a laptop opening, a
  browser restoring its session) would each send the same single-use token.
  The server handles one first; the other then presents a spent token, which
  is exactly what a stolen one looks like, and the whole session is revoked in
  every tab (ADR-0038). Found by the browser tests in Phase 17.

  A Web Lock makes the tabs take turns. The second tab's request leaves only
  after the first tab's answer has set the new cookie, so it presents the
  successor and gets its own token. Browsers without the API fall back to the
  per-tab behaviour.
*/
const REFRESH_LOCK_NAME = "flowdesk-auth-refresh";

function performRefreshAcrossTabs(): Promise<boolean> {
  if (typeof navigator === "undefined" || navigator.locks === undefined) {
    return performRefresh();
  }

  return navigator.locks.request(REFRESH_LOCK_NAME, performRefresh);
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

/**
 * Fetches a file rather than JSON.
 *
 * Goes through the same client as everything else, which is the point: the
 * download is authorised by the bearer token like any other request. A plain
 * `<a href>` would send no token and be refused, and making the file reachable
 * without one would put tenant isolation in the hands of whoever had the
 * address.
 */
export async function apiDownload(
  path: string,
  signal?: AbortSignal,
): Promise<{ blob: Blob; fileName: string | null }> {
  if (isAccessTokenStale()) {
    await refreshSession();
  }

  let response = await send(path, { signal });

  if (response.status === 401) {
    const refreshed = await refreshSession();

    if (refreshed) {
      response = await send(path, { signal });
    }
  }

  if (!response.ok) {
    const error = await toApiError(response);

    if (error.isUnauthorized) {
      clearAccessToken();
    }

    throw error;
  }

  return {
    blob: await response.blob(),
    fileName: readFileName(response.headers.get("Content-Disposition")),
  };
}

/**
 * Reads the file name the server suggested.
 *
 * Prefers the `filename*` form, which carries an encoding and is what a Turkish
 * name arrives in; the plain `filename` is the fallback for names that happen
 * to be ASCII.
 */
function readFileName(contentDisposition: string | null): string | null {
  if (contentDisposition === null) {
    return null;
  }

  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(contentDisposition);

  if (encoded?.[1] !== undefined) {
    try {
      return decodeURIComponent(encoded[1]);
    } catch {
      // A malformed header should not stop the download; the caller falls
      // back to the name it already knows.
      return null;
    }
  }

  const plain = /filename="?([^";]+)"?/i.exec(contentDisposition);

  return plain?.[1] ?? null;
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

  let body: BodyInit | undefined;

  if (options.formData !== undefined) {
    body = options.formData;
  } else if (options.body !== undefined) {
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
