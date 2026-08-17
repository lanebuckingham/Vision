import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Produces a self-contained .next/standalone directory (app + only the
  // node_modules it actually needs) so the Docker runtime image doesn't need
  // the full node_modules tree or the Next.js build toolchain. Behavior is
  // unchanged for `next dev` / `next build && next start` outside Docker.
  output: "standalone",
};

export default nextConfig;
