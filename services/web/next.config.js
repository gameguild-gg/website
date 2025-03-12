const createNextIntlPlugin = require('next-intl/plugin');
const CompressionPlugin = require('compression-webpack-plugin');
const CopyWebpackPlugin = require('copy-webpack-plugin');
const fs = require('fs');
const path = require('path');
const webpack = require('webpack');

const withNextIntl = createNextIntlPlugin();

/** @type {import('next').NextConfig} */
const nextConfig = {
  async headers() {
    return [
      {
        // Apply CORS headers to the entire site except specific paths
        source: '/((?!api/auth|api/version|disconnect|connect|wasmeriframe).*)',
        headers: [
          {
            key: 'Cross-Origin-Opener-Policy',
            value: 'same-origin',
          },
          {
            key: 'Cross-Origin-Embedder-Policy',
            value: 'credentialless',
          },
        ],
      },
      {
        // Apply no-cache headers to auth-related pages and root
        source: '/(|disconnect|connect)',
        headers: [
          {
            key: 'Cache-Control',
            value: 'no-store, no-cache, must-revalidate, proxy-revalidate',
          },
          {
            key: 'Pragma',
            value: 'no-cache',
          },
          {
            key: 'Expires',
            value: '0',
          },
          {
            key: 'Surrogate-Control',
            value: 'no-store',
          },
        ],
      },
      {
        // Apply CORS headers to API routes
        source: '/api/(auth|version)/:path*',
        headers: [
          {
            key: 'Access-Control-Allow-Origin',
            value: '*',
          },
          {
            key: 'Access-Control-Allow-Methods',
            value: 'GET, POST, OPTIONS',
          },
          {
            key: 'Access-Control-Allow-Headers',
            value: 'Content-Type, Authorization',
          },
          {
            key: 'Cross-Origin-Resource-Policy',
            value: 'cross-origin',
          },
        ],
      },
      {
        // Apply Cross-Origin-Resource-Policy to Wasmer-related routes
        source: '/wasmeriframe',
        headers: [
          {
            key: 'Cross-Origin-Resource-Policy',
            value: 'cross-origin',
          },
          {
            key: 'Cross-Origin-Opener-Policy',
            value: 'same-origin',
          },
          {
            key: 'Cross-Origin-Embedder-Policy',
            value: 'require-corp',
          },
        ],
      },
    ];
  },

  eslint: {
    ignoreDuringBuilds: true,
  },
  images: {
    remotePatterns: [
      {
        protocol: 'https',
        hostname: '**',
      },
      {
        protocol: 'http',
        hostname: '**',
      },
    ],
  },
  webpack: (config, { isServer }) => {
    if (!isServer) {
      config.module.rules.push({
        test: /\.worker\.(js|ts)$/,
        loader: 'worker-loader',
        options: {
          inline: 'fallback',
        },
      });
      config.externals = config.externals || [];
      config.externals = [...config.externals, 'apiclient'];
      config.resolve.fallback = {
        ...config.resolve.fallback,
        fs: false,
      };
    }
    config.module.rules.push(
      {
        test: /\.md$/,
        use: 'raw-loader',
      },
      {
        test: /\.wasm$/,
        type: 'asset/resource',
      },
      {
        test: /\.(pack|br|a)$/,
        type: 'asset/resource',
      },
      // {
      //   test: /\.worker\.[cm]?js$/i,
      //   use: ['worker-loader'],
      // },
      // {
      //   test: /\.worker\.ts$/,
      //   loader: 'ts-loader',
      // },
    );
    //
    config.resolve = {
      ...config.resolve,
      extensions: ['.tsx', '.ts', '.js'],
      fallback: {
        'llvm-box.wasm': false,
        'binaryen-box.wasm': false,
        'python.wasm': false,
        'quicknode.wasm': false,
        path: false,
        'node-fetch': false,
        vm: false,
      },
    };

    config.plugins.push(
      new CopyWebpackPlugin({
        patterns: [
          { from: 'public' },
          {
            from: path.resolve(__dirname, '../../node_modules/emception/brotli/brotli.wasm'),
            to: 'brotli/brotli.wasm',
          },
          {
            from: path.resolve(__dirname, '../../node_modules/emception/wasm-package/wasm-package.wasm'),
            to: 'wasm-package/wasm-package.wasm',
          },
        ],
      }),
      new CompressionPlugin({
        exclude: /\.br$/,
      }),
      new webpack.IgnorePlugin({
        resourceRegExp: /index\.mjs$/,
      }),
    );

    return config;
  },
  typescript: {
    ignoreBuildErrors: false,
  },
  experimental: {
    externalDir: false,
  },
};

// module.exports = nextConfig;
module.exports = withNextIntl(nextConfig);
