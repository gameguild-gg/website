/** @type {import('next').NextConfig} */
const nextConfig = {
    reactStrictMode: true,
	useFilesystemPublicRoutes: true,
    basePath: process.env.BASE_PATH,
	swcMinify: true,
		experimental: {
	  	esmExternals: false
	},
	eslint: {
		ignoreDuringBuilds: true,
	},
	api: {
		externalResolver: true,
	},
};