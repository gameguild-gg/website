module.exports = {
  extends: [
    "@game-guild/eslint-config"
  ],
  parserOptions: {
    tsconfigRootDir: __dirname,
    project: [
      "./packages/*/tsconfig.json",
      "./services/*/tsconfig.json",
      "./tsconfig.json"
    ]
  },
  root: true
};