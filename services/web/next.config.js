const createNextIntlPlugin = require("next-intl/plugin");

const withNextIntl = createNextIntlPlugin();

/** @type {import("next").NextConfig} */
const nextConfig = {
  output: "standalone"
};

// module.exports = nextConfig;
module.exports = withNextIntl(nextConfig);
