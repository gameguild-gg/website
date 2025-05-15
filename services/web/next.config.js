const createNextIntlPlugin = require('next-intl/plugin');
const CompressionPlugin = require('compression-webpack-plugin');
const CopyWebpackPlugin = require('copy-webpack-plugin');
const fs = require('fs');
const path = require('path');
const webpack = require('webpack');
const simpleGit = require('simple-git');

const withNextIntl = createNextIntlPlugin();

// Git stats generation function
async function generateGitStats() {
  console.log('Generating git stats during Next.js build...');
  const STATS_FILE_PATH = path.join(process.cwd(), 'git-stats.json');
  
  try {
    // Use simpleGit for cross-platform compatibility
    const git = simpleGit();

    // Get the log with stats
    let log = await git.raw(['log', '--numstat', '--format=%an']);

    // Filter out unwanted lines in a cross-platform way
    log = log
      .split('\n')
      .filter(
        (line) =>
          !line.includes('package-lock.json') &&
          !line.includes('yarn.lock') &&
          !line.includes('node_modules/'),
      )
      .join('\n');

    // Parse the log to calculate stats
    const stats = {};
    const lines = log.split('\n');

    let currentAuthor = '';
    for (const line of lines) {
      if (!line.trim()) continue;

      // If the line is an author
      if (!line.includes('\t')) {
        currentAuthor = line.trim();
        if (!stats[currentAuthor]) {
          stats[currentAuthor] = { additions: 0, deletions: 0 };
        }
      } else {
        // If the line contains stats
        const [additions, deletions] = line
          .split('\t')
          .map((x) => parseInt(x, 10) || 0);
        stats[currentAuthor].additions += additions;
        stats[currentAuthor].deletions += deletions;
      }
    }

    // remove semantic-release-bot and dependabot[bot] from stats
    delete stats['semantic-release-bot'];
    delete stats['dependabot[bot]'];

    // accumulate stats from one author to another and remove the previous author
    // map of from -> to
    const authorMap = {
      'Alexandre Tolstenko Nogueira': 'Alexandre Tolstenko',
      LMD9977: 'Nominal9977',
    };

    for (const [from, to] of Object.entries(authorMap)) {
      if (stats[from] && stats[to]) {
        stats[to].additions += stats[from].additions;
        stats[to].deletions += stats[from].deletions;
        delete stats[from];
      }
    }

    // rename name to username
    // map of name -> github username
    const usernameMap = {
      'Alexandre Tolstenko': 'tolstenko',
      'Alec Santos': 'alec-o-mago',
      'Miguel Moroni': 'migmoroni',
      'Matheus Martins': 'mathrmartins',
      Nominal9977: 'Nominal9977',
      hdorer: 'hdorer',
      'Joel Oliveira': 'vikumm',
      Germano: 'Germano123',
    };

    const newStats = [];

    for (const [name, username] of Object.entries(usernameMap)) {
      if (stats[name]) {
        newStats.push({ ...stats[name], username: username });
      }
    }

    // sort by sum of additions and deletions
    newStats.sort(
      (a, b) => b.additions + b.deletions - (a.additions + a.deletions),
    );

    // Write stats to file
    fs.writeFileSync(STATS_FILE_PATH, JSON.stringify(newStats, null, 2));
    console.log(`Git stats written to ${STATS_FILE_PATH}`);
    return newStats;
  } catch (error) {
    console.error('Error generating git stats:', error);
    return [];
  }
}

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
    );
    
    // Generate git stats during build - in production it's saved to file
    // In development, the stats are generated on-demand by the API route
    if (process.env.NODE_ENV === 'production') {
      // We'll generate stats in a separate step before webpack starts
      // This is more reliable than trying to do it within the webpack config
      // The stats will be read from the file by the API route
      
      // For now, we just need to ensure simple-git is installed
      config.plugins.push(
        new webpack.DefinePlugin({
          'process.env.GIT_STATS_GENERATED': JSON.stringify(new Date().toISOString()),
        }),
      );
    }
    
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

// Function to generate version information
async function generateVersionInfo() {
  console.log('Generating version information...');
  const VERSION_FILE_PATH = path.join(process.cwd(), 'version.json');
  
  try {
    const git = simpleGit();
    
    // Get the latest tag and commit
    const tags = await git.tags();
    const latestTag = tags.latest || 'v0.0.1';
    const commit = await git.revparse(['--short', 'HEAD']);
    
    const version = `${latestTag}.${commit.trim()}`;
    
    // Save to file for production use
    fs.writeFileSync(VERSION_FILE_PATH, JSON.stringify({ version }, null, 2));
    console.log(`Version information written to ${VERSION_FILE_PATH}: ${version}`);
    return version;
  } catch (error) {
    console.error('Error generating version information:', error);
    return 'v0.0.1';
  }
}

// Immediately Invoked Async Function Expression to generate git stats and version info before Next.js build
// This ensures the data is available when the build starts
(async () => {
  if (process.env.NODE_ENV === 'production') {
    try {
      console.log('Generating git stats before Next.js build...');
      await generateGitStats();
      console.log('Git stats generation completed successfully.');
      
      console.log('Generating version information before Next.js build...');
      await generateVersionInfo();
      console.log('Version information generation completed successfully.');
    } catch (error) {
      console.error('Failed to generate build-time data:', error);
      // Continue with the build even if generation fails
    }
  }
})();

// module.exports = nextConfig;
module.exports = withNextIntl(nextConfig);
