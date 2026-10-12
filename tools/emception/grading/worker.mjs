import { createHash } from 'node:crypto';
import { createServer } from 'node:http';
import { readFile, realpath } from 'node:fs/promises';
import { extname, resolve, sep } from 'node:path';
import { chromium } from '@playwright/test';
import { requireFrozenCodeToolchain } from './toolchain-binding.mjs';
import { createWorkerFailureDiagnostic } from './worker-diagnostics.mjs';
import { createCodeBrowserLaunchOptions } from './browser-launch-options.mjs';

// The API starts one process per frozen grading request, with no credential-bearing
// environment. Only built runtime/CDN bytes are served; submitted files stay in VFS.
const MAX_INPUT = 12_500_000;
const MAX_OUTPUT = 2_000_000;
let browser;
let server;
let phase = 'input';
let watchdog;
try {
  const [runtimeArgument, cdnArgument] = process.argv.slice(2);
  const browserOptions = createCodeBrowserLaunchOptions(process.env.GAMEGUILD_CODE_WORKER_BROWSER_CHANNEL);
  if (!runtimeArgument || !cdnArgument) throw new Error('Runtime and CDN directories are required.');
  const runtimeRoot = await realpath(runtimeArgument);
  const cdnRoot = await realpath(cdnArgument);
  let bytes = 0;
  const chunks = [];
  for await (const chunk of process.stdin) {
    bytes += chunk.length;
    if (bytes > MAX_INPUT) throw new Error('Code worker input exceeds its byte budget.');
    chunks.push(chunk);
  }
  const raw = Buffer.concat(chunks);
  const requestHash = createHash('sha256').update(raw).digest('hex');
  const request = JSON.parse(raw.toString('utf8'));
  if (request.schemaVersion !== 1 || !request.definition || !request.files) throw new Error('Invalid Code worker request.');
  phase = 'artifact verification';
  const manifestBytes = await readFile(resolve(cdnRoot, 'manifest.json'));
  requireFrozenCodeToolchain(request.toolchain, JSON.parse(manifestBytes));
  const manifestHash = createHash('sha256').update(manifestBytes).digest('hex');

  watchdog = setTimeout(() => {
    void browser?.close();
    server?.close();
    process.stderr.write(`Code worker deadline exceeded during ${phase}.\n`);
    process.exitCode = 1;
  }, 300_000);

  phase = 'runtime server';
  server = createServer(async (req, res) => {
    res.setHeader('Cross-Origin-Opener-Policy', 'same-origin');
    res.setHeader('Cross-Origin-Embedder-Policy', 'require-corp');
    res.setHeader('Cache-Control', 'no-store');
    try {
      const url = new URL(req.url, 'http://localhost');
      if (url.pathname === '/') {
        res.setHeader('Content-Type', 'text/html');
        res.end('<!doctype html><script type="module">import {executeCodeAssessment} from "/runtime/grading.js";globalThis.executeCodeAssessment=executeCodeAssessment;</script>');
        return;
      }
      const root = url.pathname.startsWith('/runtime/') ? runtimeRoot
        : url.pathname.startsWith('/cdn/') ? cdnRoot : undefined;
      if (!root) { res.writeHead(404).end(); return; }
      const relative = decodeURIComponent(url.pathname.slice(url.pathname.startsWith('/runtime/') ? 9 : 5));
      const path = await realpath(resolve(root, relative));
      if (!path.startsWith(root + sep)) { res.writeHead(403).end(); return; }
      const types = { '.js': 'text/javascript', '.mjs': 'text/javascript', '.wasm': 'application/wasm', '.json': 'application/json' };
      res.setHeader('Content-Type', types[extname(path)] ?? 'application/octet-stream');
      res.end(await readFile(path));
    } catch { res.writeHead(404).end(); }
  });
  await new Promise((resolveListen, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolveListen);
  });
  const origin = `http://127.0.0.1:${server.address().port}`;
  phase = 'browser startup';
  browser = await chromium.launch(browserOptions);
  const context = await browser.newContext({ serviceWorkers: 'block', acceptDownloads: false });
  await context.route('**/*', (route) => {
    const url = new URL(route.request().url());
    return url.origin === origin ? route.continue() : route.abort('blockedbyclient');
  });
  await context.routeWebSocket('**/*', (socket) => socket.close());
  const page = await context.newPage();
  page.on('pageerror', () => { void browser.close(); });
  // Raw compiler/student output and private diagnostics are not process receipts.
  let outputBytes = 0;
  page.on('console', (message) => {
    outputBytes += Buffer.byteLength(message.text());
    if (outputBytes > MAX_OUTPUT) void browser.close();
  });
  await page.goto(origin, { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => typeof globalThis.executeCodeAssessment === 'function');
  phase = 'WebAssembly evaluation';
  const passed = await page.evaluate((input) => globalThis.executeCodeAssessment(input), request);
  if (!Array.isArray(passed) || passed.length > 100 || passed.some((value) => typeof value !== 'boolean')) {
    throw new Error('Invalid Code execution report.');
  }
  process.stdout.write(JSON.stringify({ schemaVersion: 1, requestHash, manifestHash, passed }));
} catch (error) {
  process.stderr.write(createWorkerFailureDiagnostic(phase, error) + '\n');
  process.exitCode = 1;
} finally {
  clearTimeout(watchdog);
  await browser?.close();
  if (server) await new Promise((closed) => server.close(closed));
}
