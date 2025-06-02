/**
 * @see https://jamiemason.github.io/syncpack/config/syncpackrc/
 * @type {import('syncpack').RcFile}
 */
const config = {
  sortFirst: ['name', 'description', 'version', 'author', 'license', 'private', 'type', 'workspaces', 'scripts', 'dependencies', 'devDependencies'],
  sortPackages: true,
};

export default config;
