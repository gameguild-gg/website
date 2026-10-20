import react from '@vitejs/plugin-react';
import { readFile } from 'node:fs/promises';
import type { IncomingMessage, ServerResponse } from 'node:http';
import path from 'node:path';
import { defineConfig, type Plugin } from 'vite';

function emceptionRawCdnPlugin(): Plugin {
    let publicDir = '';

    const handler = async (req: IncomingMessage, res: ServerResponse, next: () => void) => {
        const reqUrl = req.url ? String(req.url).split('?')[0] : '';
        if (!reqUrl.startsWith('/cdn/') || (!reqUrl.endsWith('.br') && !reqUrl.endsWith('.gz'))) {
            next();
            return;
        }

        try {
            const relativePath = reqUrl.replace(/^\/+/, '');
            const resolvedPublicDir = path.resolve(publicDir);
            const absolutePath = path.resolve(resolvedPublicDir, relativePath);
            const publicPrefix = `${resolvedPublicDir}${path.sep}`;
            if (!absolutePath.startsWith(publicPrefix)) {
                res.statusCode = 403;
                res.end('Forbidden');
                return;
            }

            const data = await readFile(absolutePath);
            res.statusCode = 200;
            res.setHeader('Content-Type', 'application/octet-stream');
            res.setHeader('Content-Length', String(data.length));
            res.setHeader('Cache-Control', 'no-cache');
            // Intentionally omit Content-Encoding to prevent automatic browser decompression.
            res.end(data);
        } catch {
            next();
        }
    };

    return {
        name: 'emception-raw-cdn-bundles',
        configResolved(config) {
            publicDir = config.publicDir;
        },
        configureServer(server) {
            server.middlewares.use(handler);
        },
        configurePreviewServer(server) {
            server.middlewares.use(handler);
        },
    };
}

export default defineConfig({
    plugins: [react(), emceptionRawCdnPlugin()],
    // Set base to repo name for GitHub Pages deployment under a subpath.
    // Override via VITE_BASE env var if needed: VITE_BASE=/custom/ vite build
    base: process.env.VITE_BASE || '/',
    worker: {
        format: 'es',
    },
    // Exclude CDN sysroot files (contains .html docs) from dependency scanning
    optimizeDeps: {
        exclude: [
            '@gameguild/emception-ide',
            '@gameguild/emception-browser',
            '@gameguild/emception-xterm',
            'emception'
        ],
        entries: ['index.html', 'src/**/*.{ts,tsx}'],
    },
    server: {
        headers: {
            // Required for SharedArrayBuffer (used by Emscripten threads / WASM).
            'Cross-Origin-Opener-Policy': 'same-origin',
            'Cross-Origin-Embedder-Policy': 'require-corp',
        },
    },
});
