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
        test: /\.wasm$/,
        type: 'asset/resource',
        generator: {
          filename: 'static/wasm/[name].[hash][ext]',
        },
      },
      {
        test: /\.(pack|br|a|tar)$/,
        type: 'asset/resource',
        generator: {
          filename: 'static/assets/[name].[hash][ext]',
        },
      },
    );

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

    // Check for git-stats.json and prepare copy patterns
    const copyPatterns = [
      { from: 'public' },
      {
        from: path.resolve(__dirname, '../../node_modules/emception/brotli/brotli.wasm'),
        to: 'brotli/brotli.wasm',
      },
      {
        from: path.resolve(__dirname, '../../node_modules/emception/wasm-package/wasm-package.wasm'),
        to: 'wasm-package/wasm-package.wasm',
      },
    ];

    // Path to source git-stats.json file
    const gitStatsSourcePath = path.resolve(__dirname, '../../git-stats.json');
    const gitStatsDestPath = path.resolve(__dirname, 'git-stats.json');

    // Check if source file exists
    if (fs.existsSync(gitStatsSourcePath)) {
      let shouldCopy = true;

      // Check if destination exists and compare timestamps
      if (fs.existsSync(gitStatsDestPath)) {
        const sourceStats = fs.statSync(gitStatsSourcePath);
        const destStats = fs.statSync(gitStatsDestPath);

        // Only copy if source is newer than destination
        shouldCopy = sourceStats.mtime > destStats.mtime;
      }

      // Add to copy patterns if needed
      if (shouldCopy) {
        console.log('Copying git-stats.json as it is newer or destination is missing');
        copyPatterns.push({
          from: gitStatsSourcePath,
          to: gitStatsDestPath,
        });
      }
    } else {
      console.warn('git-stats.json not found at project root, skipping copy.');
    }

    // Always add the CopyWebpackPlugin with the prepared patterns
    config.plugins.push(
      new CopyWebpackPlugin({
        patterns: copyPatterns,
      }),
    );

    // Add compression plugin for production
    if (process.env.NODE_ENV === 'production') {
      config.plugins.push(
        new CompressionPlugin({
          filename: '[path][base].gz',
          algorithm: 'gzip',
          test: /\.(js|css|html|svg)$/,
          threshold: 10240,
          minRatio: 0.8,
        }),
      );
    }

    return config;
  },
  typescript: {
    ignoreBuildErrors: false,
  },
  transpilePackages: ['use-pyodide', '@radix-ui/react-slot'],
  experimental: {
    externalDir: false,
  },
};

module.exports = withNextIntl(nextConfig);
