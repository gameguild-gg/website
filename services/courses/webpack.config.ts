const CompressionPlugin = require("compression-webpack-plugin");
const CopyWebpackPlugin = require("copy-webpack-plugin");
const path = require("path");

// todo: type this
// import { Configuration } from "webpack";
const config = {
  mode:
    (process.env.NODE_ENV as "production" | "development" | undefined) ??
    "development",
  entry: "./src/entrypoint.tsx",
  module: {
    rules: [
      {
        test: /.tsx?$/,
        use: "ts-loader",
        exclude: /node_modules/,
      },
      {
        test: /\.css$/,
        use: ["style-loader", "css-loader"],
      },
      {
        test: /\.wasm$/,
        type: "asset/resource",
      },
      {
        test: /\.(pack|br|a)$/,
        type: "asset/resource",
      },
      {
        test: /\.worker\.m?js$/,
        exclude: /monaco-editor/,
        use: ["worker-loader"],
      },
      {
        test: /\.worker\.ts$/,
        loader: "ts-loader",
        // options: {
        //   publicPath: "/scripts/workers/",
        // },
      },
      // {
      //   test: /\.worker\.ts$/,
      //   use: [{ loader: 'worker-loader' }, { loader: 'ts-loader' }],
      // },
    ],
  },
  resolve: {
    extensions: [".tsx", ".ts", ".js"],
    // alias: {
    //   emception: "../build/emception",
    // },
    fallback: {
      "llvm-box.wasm": false,
      "binaryen-box.wasm": false,
      "python.wasm": false,
      "quicknode.wasm": false,
      path: false,
      "node-fetch": false,
      vm: false,
    },
  },
  output: {
    filename: "bundle.js",
    path: path.resolve(__dirname, "dist"),
  },
  plugins: [
    new CopyWebpackPlugin({
      patterns: [
        { from: "public" },
        {
          from: "./node_modules/emception/brotli/brotli.wasm",
          to: "brotli/brotli.wasm",
        },
        {
          from: "./node_modules/emception/wasm-package/wasm-package.wasm",
          to: "wasm-package/wasm-package.wasm",
        },
      ],
    }),
    new CompressionPlugin({
      exclude: /\.br$/,
    }),
  ],
  devServer: {
    allowedHosts: "auto",
    port: "auto",
    server: "https",
    headers: {
      "Cross-Origin-Embedder-Policy": "require-corp",
      "Cross-Origin-Opener-Policy": "same-origin",
    },
  },
};

module.exports = config;
