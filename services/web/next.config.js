/** @type {import('next').NextConfig} */
const nextConfig = {
  output: 'standalone',
  ignoreBuildErrors: true,
  eslint: {
    ignoreDuringBuilds: true,
  },
};

module.exports = nextConfig;
