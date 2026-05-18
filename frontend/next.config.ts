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
  turbopack: { root: projectRoot },
  async rewrites() {
    const backendUrl = process.env.API_URL ?? "http://localhost:5138";
    return [
      {
        source: "/api/:path*",
        destination: `${backendUrl}/api/:path*`,
      },
    ];
  },
};

export default withBundleAnalyzer(nextConfig);
