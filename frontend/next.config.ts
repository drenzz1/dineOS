import path from "node:path";
import { fileURLToPath } from "node:url";
import type { NextConfig } from "next";
import bundleAnalyzer from "@next/bundle-analyzer";

const projectRoot = path.dirname(fileURLToPath(import.meta.url));

const withBundleAnalyzer = bundleAnalyzer({
  enabled: process.env.ANALYZE === "true",
  analyzerMode: "static",
  openAnalyzer: false,
});

// Security headers (M5.5 — applied after the OWASP ZAP baseline flagged missing
// CSP, anti-clickjacking, Permissions-Policy, and X-Content-Type-Options headers).
// Applied to every response via headers() below. The CSP is a non-nonce baseline:
// it keeps 'unsafe-inline'/'unsafe-eval' on scripts (Next's hydration bootstrap
// needs them without a nonce) while still locking down frame-ancestors, object-src,
// base-uri, and form-action. A nonce-based strict CSP is the documented follow-up.
// connect-src allows wss: for the same-origin SignalR hub; Stripe is redirect-based
// (full-page navigation to checkout.stripe.com) so it needs no frame/script grant.
const securityHeaders = [
  {
    key: "Content-Security-Policy",
    value: [
      "default-src 'self'",
      "base-uri 'self'",
      "font-src 'self' data:",
      "form-action 'self'",
      "frame-ancestors 'none'",
      "frame-src 'self'",
      "img-src 'self' data: blob: https:",
      "object-src 'none'",
      "script-src 'self' 'unsafe-inline' 'unsafe-eval'",
      "style-src 'self' 'unsafe-inline'",
      "connect-src 'self' https: wss:",
      "worker-src 'self' blob:",
    ].join("; "),
  },
  { key: "X-Frame-Options", value: "DENY" },
  { key: "X-Content-Type-Options", value: "nosniff" },
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  {
    key: "Permissions-Policy",
    value: "camera=(), microphone=(), geolocation=(), browsing-topics=()",
  },
  { key: "Strict-Transport-Security", value: "max-age=31536000" },
  { key: "Cross-Origin-Opener-Policy", value: "same-origin" },
  { key: "Cross-Origin-Resource-Policy", value: "same-origin" },
];

const nextConfig: NextConfig = {
  output: "standalone",
  // Don't advertise the framework/version (ZAP: "Server Leaks Information via
  // X-Powered-By"). Removes the `X-Powered-By: Next.js` response header.
  poweredByHeader: false,
  turbopack: { root: projectRoot },
  // Performance (M5.4): serve modern, smaller formats from the built-in image
  // optimizer — AVIF first (best compression), WebP fallback; legacy browsers
  // still get the original. minimumCacheTTL keeps optimized variants cached
  // ~31 days so repeat views skip re-encoding.
  images: {
    formats: ["image/avif", "image/webp"],
    minimumCacheTTL: 2678400,
  },
  async rewrites() {
    const backendUrl = process.env.API_INTERNAL_URL ?? "http://localhost:5138";
    return [
      {
        source: "/api/:path*",
        destination: `${backendUrl}/api/:path*`,
      },
      {
        // The SignalR hub is mapped at the backend's top-level /hubs/* (NOT
        // under /api), so it needs its own proxy rule. Without this, the hub
        // negotiate request 404s and realtime kitchen updates never arrive.
        source: "/hubs/:path*",
        destination: `${backendUrl}/hubs/:path*`,
      },
    ];
  },
  async headers() {
    return [{ source: "/:path*", headers: securityHeaders }];
  },
};

export default withBundleAnalyzer(nextConfig);
