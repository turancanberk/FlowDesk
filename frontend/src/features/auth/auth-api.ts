import { apiFetch, refreshSession } from "@/lib/api/http-client";
import { clearAccessToken, setAccessToken } from "@/lib/api/access-token-store";
import type { CurrentUser, SessionResponse } from "./auth-types";

/*
  Every call to an authentication endpoint goes through here.

  `skipAuthRefresh` is set on all of them: these endpoints establish or end a
  session, so letting the client try to refresh on their behalf would either
  loop or spend the refresh token on a request that does not need it.
*/

export async function register(input: {
  email: string;
  displayName: string;
  password: string;
}): Promise<CurrentUser> {
  const session = await apiFetch<SessionResponse>("/api/auth/register", {
    method: "POST",
    body: input,
    skipAuthRefresh: true,
  });

  setAccessToken(session.accessToken, session.accessTokenExpiresAt);

  return session.user;
}

export async function login(input: { email: string; password: string }): Promise<CurrentUser> {
  const session = await apiFetch<SessionResponse>("/api/auth/login", {
    method: "POST",
    body: input,
    skipAuthRefresh: true,
  });

  setAccessToken(session.accessToken, session.accessTokenExpiresAt);

  return session.user;
}

export async function logout(): Promise<void> {
  try {
    await apiFetch<void>("/api/auth/logout", { method: "POST", skipAuthRefresh: true });
  } finally {
    // Clear locally even if the call failed. The user asked to sign out, and
    // leaving a usable token in memory because the network hiccuped would be
    // the wrong way to fail.
    clearAccessToken();
  }
}

export function fetchCurrentUser(signal?: AbortSignal): Promise<CurrentUser> {
  return apiFetch<CurrentUser>("/api/me", { signal });
}

/**
 * Rebuilds the session from the refresh cookie on application start.
 *
 * The access token lives only in memory, so a page load always begins signed
 * out from the client's point of view (ADR-0009). This is what turns a valid
 * cookie back into a usable session.
 */
export async function restoreSession(): Promise<CurrentUser | null> {
  const refreshed = await refreshSession();

  if (!refreshed) {
    return null;
  }

  try {
    return await fetchCurrentUser();
  } catch {
    clearAccessToken();
    return null;
  }
}
