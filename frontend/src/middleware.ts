import { NextResponse } from "next/server";

import { API_BASE_URL } from "@/lib/api/api-origin";

/*
  Content-Security-Policy for the documents the app serves.

  A nonce rather than 'unsafe-inline': Next puts inline bootstrap scripts in
  every page, and allowing inline script would leave the policy with nothing
  to say about the attack it exists to stop. The nonce is generated per
  request and attached to Next's own scripts (docs/SECURITY.md §14).
*/
export function middleware(): NextResponse {
  const nonce = crypto.randomUUID().replaceAll("-", "");

  /*
    The API lives on another origin in development; behind Caddy in production
    it is the same one. Both are allowed to be connected to, nothing else is.

    The same value the HTTP client sends to (api-origin.ts). Reading the
    variable separately here is what once let the two disagree.
  */
  const apiOrigin = API_BASE_URL;

  /*
    React uses eval in development — and only there — to rebuild server-side
    error stacks in the browser. Without it every page logs "eval() is not
    supported in this environment" and those stacks are lost. Neither React nor
    Next uses eval in a production build, so the production policy stays
    without it; the browser tests run against that build and assert as much.

    Keyed on NODE_ENV, which `next build` and `next start` always set to
    "production": there is no configuration by which a deployed bundle takes
    this branch.
  */
  const evalInDevelopment = process.env.NODE_ENV === "development" ? " 'unsafe-eval'" : "";

  const policy = [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'${evalInDevelopment}`,
    /*
      Style stays inline-friendly. Base UI positions menus, dialogs and
      popovers with style attributes, and a policy that forbids them would
      break the interface without making script injection any harder.
    */
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob:",
    "font-src 'self'",
    `connect-src 'self' ${apiOrigin}`.trim(),
    "frame-ancestors 'none'",
    "form-action 'self'",
    "base-uri 'none'",
    "object-src 'none'",
  ].join("; ");

  /*
    Next reads the policy off this response, takes the nonce out of it and
    stamps it on every script it renders — so the page's own bootstrap script
    is allowed and nothing else is.
  */
  const response = NextResponse.next();
  response.headers.set("Content-Security-Policy", policy);

  return response;
}

export const config = {
  /*
    Documents only. Static assets and images are served with the headers from
    next.config.ts; a per-request nonce on a cached file would be a nonce
    that means nothing.
  */
  matcher: [
    {
      source: "/((?!_next/static|_next/image|favicon.ico).*)",
      missing: [
        { type: "header", key: "next-router-prefetch" },
        { type: "header", key: "purpose", value: "prefetch" },
      ],
    },
  ],
};
