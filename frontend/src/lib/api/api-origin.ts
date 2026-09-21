/*
  Where the API is, decided in one place.

  Two things need this answer and they must agree: the HTTP client, which sends
  the requests, and the middleware, which writes the `connect-src` the browser
  will hold those requests to. When they disagreed — the client defaulting to
  localhost and the policy listing nothing — the sign-in form loaded perfectly
  and every request it made was refused, with the reason visible only in the
  browser console (Faz 23).

  `NEXT_PUBLIC_*` is read at build time, so this is the value the deployed
  bundle carries. In production the API is behind the same origin as the app
  (ADR-0044) and the variable is empty there on purpose: an empty origin adds
  nothing to `connect-src`, and `'self'` already covers it.
*/

/** The default for local development, where the API runs on its own port. */
export const DEVELOPMENT_API_ORIGIN = "http://localhost:5080";

/**
 * The API's base address: what is configured, or the development default.
 *
 * Empty only when the variable is explicitly set to an empty string, which is
 * how a same-origin deployment says "no other origin is involved".
 */
export const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? DEVELOPMENT_API_ORIGIN;
