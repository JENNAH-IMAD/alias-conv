import type { NextConfig } from 'next';
import { networkInterfaces } from 'node:os';

const nextConfig: NextConfig = {
  output: 'standalone',
  // Allow 10 minutes of queueing + 10 minutes of OCR, with time to save the result.
  experimental: { proxyTimeout: 1_260_000, proxyClientMaxBodySize: Number.MAX_SAFE_INTEGER },
  allowedDevOrigins: Object.values(networkInterfaces())
    .flatMap((addresses) => addresses ?? [])
    .filter((address) => address.family === 'IPv4' && !address.internal)
    .map((address) => address.address),
  async redirects() {
    return [{ source: '/register', destination: '/login', permanent: false }];
  },
  // Le navigateur utilise la même origine depuis chaque poste du réseau.
  async rewrites() {
    const backend = (process.env.API_PROXY_URL || 'http://127.0.0.1:5081').replace(/\/$/, '');
    return [{ source: '/api/:path*', destination: `${backend}/api/:path*` }];
  },
  // La version Windows se compile séparément du serveur de développement.
  distDir:
    process.env.ALIAS_RUNTIME_BUILD === '1'
      ? '.next-runtime'
      : process.env.ALIAS_DESKTOP_BUILD === '1'
        ? '.next-desktop'
        : '.next',
};

export default nextConfig;
