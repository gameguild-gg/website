/** @type {import('next').NextConfig} */
const nextConfig = {
    reactStrictMode: true,
    basePath: process.env.BASE_PATH,
		swcMinify: true,
	experimental: {
	  esmExternals: false
	}
 }; 