import type { NextConfig } from "next";

/*
  Security headers for every document the frontend serves
  (docs/SECURITY.md §14). Content-Security-Policy is not here: it carries a
  per-request nonce and is set in middleware.ts.

  Set by the application rather than only by the proxy in front of it, so
  development, the browser tests and production all serve the same headers.
*/
const securityHeaders = [
  { key: "X-Content-Type-Options", value: "nosniff" },
  { key: "X-Frame-Options", value: "DENY" },
  // The path carries the workspace address and record ids; no other site
  // needs to learn them from a referrer.
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  {
    key: "Permissions-Policy",
    value:
      "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()",
  },
];

// Only where there is TLS to insist on. On http://localhost a stray max-age
// would pin every other project on the machine to https.
const productionHeaders =
  process.env.NODE_ENV === "production" && process.env["FLOWDESK_ENABLE_HSTS"] === "true"
    ? [{ key: "Strict-Transport-Security", value: "max-age=31536000; includeSubDomains" }]
    : [];

const nextConfig: NextConfig = {
  async headers() {
    return [{ source: "/:path*", headers: [...securityHeaders, ...productionHeaders] }];
  },
};

export default nextConfig;
