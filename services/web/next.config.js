const createNextIntlPlugin = require('next-intl/plugin');
const CompressionPlugin = require('compression-webpack-plugin');
const CopyWebpackPlugin = require('copy-webpack-plugin');

const withNextIntl = createNextIntlPlugin();

/** @type {import("next").NextConfig} */
const nextConfig = {
  output: 'standalone',
  eslint: {
    ignoreDuringBuilds: true,
  },
  webpack: (config) => {
    // Add custom rules
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
        test: /\.worker\.m?js$/,
        exclude: /monaco-editor/,
        use: ['worker-loader'],
      },
      {
        test: /\.worker\.ts$/,
        loader: 'ts-loader',
      },
    );

    // Add custom plugins
    config.plugins.push(
      new CopyWebpackPlugin({
        patterns: [
          { from: 'public' },
          {
            from: '../../node_modules/emception/brotli/brotli.wasm',
            to: 'brotli/brotli.wasm',
          },
          {
            from: '../../node_modules/emception/wasm-package/wasm-package.wasm',
            to: 'wasm-package/wasm-package.wasm',
          },
        ],
      }),
      new CompressionPlugin({
        exclude: /\.br$/,
      }),
    );

    // Add custom resolve fallback
    config.resolve.fallback = {
      ...config.resolve.fallback,
      'llvm-box.wasm': false,
      'binaryen-box.wasm': false,
      'python.wasm': false,
      'quicknode.wasm': false,
      path: false,
      'node-fetch': false,
      vm: false,
    };

    return config;
  },
};

// module.exports = nextConfig;
module.exports = withNextIntl(nextConfig);
