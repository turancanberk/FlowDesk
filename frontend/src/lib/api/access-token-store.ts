/*
  In-memory store for the access token.

  The token lives in a module-scoped variable and nowhere else. It is never
  written to localStorage, sessionStorage, IndexedDB or a cookie readable by
  script (ADR-0006).

  The reason is blast radius. Persistent storage is readable by any script on
  the origin, so one cross-site scripting flaw turns into a token an attacker
  keeps. A value held in memory dies with the tab, and the session is rebuilt
  from the HttpOnly refresh cookie on the next load — which script cannot read
  at all.

  The cost is a silent refresh on every page load. That is the trade we chose.
*/

let accessToken: string | null = null;
let expiresAt: number | null = null;

/**
 * Refresh slightly before the token actually expires, so an in-flight request
 * does not arrive just after the server stops accepting it.
 */
const EXPIRY_MARGIN_MS = 30_000;

type Listener = (token: string | null) => void;

const listeners = new Set<Listener>();

export function setAccessToken(token: string, tokenExpiresAt: string | Date): void {
  accessToken = token;
  expiresAt = new Date(tokenExpiresAt).getTime();
  notify();
}

export function clearAccessToken(): void {
  accessToken = null;
  expiresAt = null;
  notify();
}

export function getAccessToken(): string | null {
  return accessToken;
}

/** True when there is no token, or the one we have is about to stop working. */
export function isAccessTokenStale(now: number = Date.now()): boolean {
  if (accessToken === null || expiresAt === null) {
    return true;
  }

  return now >= expiresAt - EXPIRY_MARGIN_MS;
}

/** Lets React components react to sign-in and sign-out without polling. */
export function subscribeToAccessToken(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

function notify(): void {
  for (const listener of listeners) {
    listener(accessToken);
  }
}
