const createNextIntlPlugin = require('next-intl/plugin');
const CompressionPlugin = require('compression-webpack-plugin');
const CopyWebpackPlugin = require('copy-webpack-plugin');
const fs = require('fs');
const path = require('path');

const withNextIntl = createNextIntlPlugin();

/** @type {import("next").NextConfig} */
const nextConfig = {
  eslint: {
    ignoreDuringBuilds: true,
  },
  webpack: (config, { isServer }) => {
    if (!isServer) {
      config.externals = config.externals || [];
      config.externals = [...config.externals, 'apiclient'];
    }
    config.module.rules.push(
      {
        test: /\.wasm$/,
        type: 'asset/resource',
      },
      {
        test: /\.(pack|br|a)$/,
        type: 'asset/resource',
      },
      {
        test: /\.worker\.[cm]?js$/i,
        use: ['worker-loader'],
      },
      {
        test: /\.worker\.ts$/,
        loader: 'ts-loader',
      },
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
    //
    config.plugins.push(
      new CopyWebpackPlugin({
        patterns: [
          { from: 'public' },
          {
            from: path.resolve(
              __dirname,
              '../../node_modules/emception/brotli/brotli.wasm',
            ),
            to: 'brotli/brotli.wasm',
          },
          {
            from: path.resolve(
              __dirname,
              '../../node_modules/emception/wasm-package/wasm-package.wasm',
            ),
            to: 'wasm-package/wasm-package.wasm',
          },
        ],
      }),
      new CompressionPlugin({
        exclude: /\.br$/,
      }),
    );

    return config;
  },
  // typescript: {
  //   ignoreBuildErrors: false,
  // },
  experimental: {
    externalDir: false,
  },
};

// module.exports = nextConfig;
module.exports = withNextIntl(nextConfig);
