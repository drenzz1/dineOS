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

const nextConfig: NextConfig = {
  output: "standalone",
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
};

export default withBundleAnalyzer(nextConfig);
