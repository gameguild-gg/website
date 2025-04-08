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
      {
        // Special CORS rules for external script loading
        source: '/:path*',
        headers: [
          {
            key: 'Access-Control-Allow-Origin',
            value: '*',
          },
        ],
      },
      {
        // Add cache headers for WebAssembly and TAR files
        source: '/assets/:path*.(wasm|tar)',
        headers: [
          {
            key: 'Cache-Control',
            value: 'public, max-age=31536000, immutable',
          },
          {
            key: 'Expires',
            value: new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toUTCString(),
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
        test: /\.(pack|br|a|tar|wasm)$/,
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

    // Always add the CopyWebpackPlugin
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
      new webpack.IgnorePlugin({
        resourceRegExp: /index\.mjs$/,
      })
    );
    
    // Only add compression plugins in production mode to avoid slowing down development
    if (process.env.NODE_ENV === 'production') {
      console.log('Enabling compression plugins for production build...');
      config.plugins.push(
        new CompressionPlugin({
          test: /\.(js|css|html|svg|json|wasm|jpg|png|ttf|otf|woff|woff2)$/,
          filename: '[path][base].gz',
          algorithm: 'gzip',
          threshold: 10240, // Only compress files larger than 10kb
          minRatio: 0.8,
          exclude: /\.br$/,
        }),
        // Add Brotli compression plugin
        new CompressionPlugin({
          filename: '[path][base].br',
          algorithm: 'brotliCompress',
          test: /\.(js|css|html|svg|json|wasm|jpg|png|ttf|otf|woff|woff2)$/,
          compressionOptions: { level: 11 },
          threshold: 10240,
          minRatio: 0.8,
          exclude: /\.br$/,
        })
      );
    } else {
      console.log('Compression plugins disabled for development mode.');
    }

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
