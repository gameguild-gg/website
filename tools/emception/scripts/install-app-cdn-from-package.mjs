import { cp, mkdir, rm, stat } from 'node:fs/promises';
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { stageCdnPackage } from './lib/stage-cdn-package.mjs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const emceptionRoot = path.resolve(__dirname, '..');
const require = createRequire(import.meta.url);

const appArgs = process.argv.slice(2);
const defaultApps = [
    '../../demos/emception-ide-next',
    '../../demos/emception-ide-react',
    '../../demos/emception-run-react',
    '../../demos/emception-run-webcomponent',
];

async function exists(targetPath) {
    try {
        await stat(targetPath);
        return true;
    } catch {
        return false;
    }
}

function resolveAppDir(appArg) {
    if (path.isAbsolute(appArg)) {
        return appArg;
    }

    return path.resolve(emceptionRoot, appArg);
}

function resolveInstalledEmceptionManifest(appDir) {
    return require.resolve('emception/cdn/manifest.json', {
        paths: [appDir],
    });
}

async function ensureCoreCdnStaged() {
    const targetCdn = path.join(emceptionRoot, 'packages', 'core', 'cdn');
    const manifest = path.join(targetCdn, 'manifest.json');
    if (await exists(manifest)) return;

    const sourceCdn = path.join(emceptionRoot, 'artifacts', 'toolchain', 'release', 'cdn');
    if (!(await exists(path.join(sourceCdn, 'manifest.json')))) {
        throw new Error(
            `Missing ${path.relative(emceptionRoot, sourceCdn)}. Run \`pnpm toolchain release\` or fetch the release artifacts first.`,
        );
    }
    const result = await stageCdnPackage({ sourceCdn, targetCdn });
    console.log(`[install-app-cdn] staged core cdn: ${result.bundleCount} bundles, ${result.totalBytes} bytes`);
}

async function installCdnForApp(appArg) {
    const appDir = resolveAppDir(appArg);
    const appLabel = path.relative(emceptionRoot, appDir) || appArg;

    await ensureCoreCdnStaged();

    const sourceManifest = resolveInstalledEmceptionManifest(appDir);
    const sourceCdnDir = path.dirname(sourceManifest);

    if (!(await exists(sourceManifest))) {
        throw new Error(
            `Missing ${path.relative(emceptionRoot, sourceManifest)}. Ensure installed package \"emception\" contains cdn assets.`,
        );
    }

    const targetCdnDir = path.join(appDir, 'public', 'cdn');
    await mkdir(path.dirname(targetCdnDir), { recursive: true });
    await rm(targetCdnDir, { recursive: true, force: true });

    await cp(sourceCdnDir, targetCdnDir, {
        recursive: true,
        force: true,
        dereference: true,
    });

    console.log(
        `[install-app-cdn] ${path.relative(emceptionRoot, sourceCdnDir)} -> ${path.relative(emceptionRoot, targetCdnDir)} (${appLabel})`,
    );
}

async function main() {
    const targets = appArgs.length ? appArgs : defaultApps;

    for (const target of targets) {
        await installCdnForApp(target);
    }
}

main().catch((error) => {
    console.error('[install-app-cdn] Failed:', error);
    process.exitCode = 1;
});
