import baseConfig from '@game-guild/eslint-config';
import nextConfig from 'eslint-config-next';

/** @type {import('eslint').Linter.Config[]} */
const config = [...baseConfig, ...nextConfig];

export default config;
