/**
 * Validates a post-sign-in redirect target.
 *
 * The value comes from the query string, so it is attacker-controlled. Without
 * this check, a link such as `/giris?next=https://saldirgan.example` would send
 * someone to another site immediately after they signed in — a convincing place
 * to ask for a password again. Only same-origin paths are allowed through, and
 * anything else falls back to the home page.
 *
 * Protocol-relative values (`//host`) and backslash variants are rejected too:
 * browsers treat both as absolute URLs.
 */
export function toSafeRedirect(value: string | null | undefined, fallback = "/"): string {
  if (value === null || value === undefined || value.length === 0) {
    return fallback;
  }

  if (!value.startsWith("/")) {
    return fallback;
  }

  if (value.startsWith("//") || value.startsWith("/\\")) {
    return fallback;
  }

  return value;
}
