// Hosted acceptance source; not executed locally.
import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import path from 'node:path';

export async function verifyMetadataRoutes({ webUrl, page, evidence }) {
  assert.equal(process.env.GITHUB_ACTIONS, 'true');
  const origin = new URL(webUrl);
  assert.equal(origin.protocol, 'http:');
  assert.equal(origin.hostname, '127.0.0.1');
  assert.ok(Number(origin.port) > 0);
  const cases = [];
  const responses = [];
  const passed = name => cases.push({ name, outcome: 'Passed' });
  const readRoute = async (pathname, mediaType) => {
    const url = new URL(pathname, origin);
    assert.equal(url.origin, origin.origin);
    const response = await fetch(url, { redirect: 'manual', signal: AbortSignal.timeout(120_000) });
    assert.equal(response.status, 200, pathname);
    assert.match(response.headers.get('content-type') ?? '', mediaType);
    const text = await response.text();
    assert.ok(Buffer.byteLength(text) < 1_000_000, 'Metadata response is unexpectedly large.');
    responses.push({ pathname, status: response.status, contentType: response.headers.get('content-type') });
    return text;
  };
  try {
    const links = await page.locator('link[rel="manifest"]').evaluateAll(nodes => nodes.map(node => node.href));
    assert.equal(links.filter(link => new URL(link).href === new URL('/manifest.webmanifest', origin).href).length, 1);
    passed('The rendered application layout links the generated manifest route');

    const manifest = JSON.parse(await readRoute('/manifest.webmanifest', /^application\/manifest\+json\b/i));
    assert.equal(manifest.name, 'GameGuild');
    assert.equal(manifest.short_name, 'GameGuild');
    assert.equal(manifest.start_url, '/');
    assert.equal(manifest.display, 'standalone');
    assert.equal(typeof manifest.description, 'string');
    assert.ok(manifest.description.length > 0);
    assert.deepEqual(manifest.icons, [{ src: '/favicon.svg', sizes: 'any', type: 'image/svg+xml' }]);
    const svg = await readRoute('/favicon.svg', /^image\/svg\+xml\b/i);
    assert.match(svg, /<svg\b/);
    passed('Next serves the generated GameGuild manifest and its referenced SVG icon over HTTP');

    const robots = await readRoute('/robots.txt', /^text\/plain\b/i);
    const lines = robots.split(/\r?\n/).map(line => line.trim());
    for (const expected of ['User-Agent: *', 'Allow: /', 'Disallow: /api/', 'Disallow: /dashboard/', `Sitemap: ${origin.origin}/sitemap.xml`]) {
      assert.ok(lines.some(line => line.toLowerCase() === expected.toLowerCase()), expected);
    }
    passed('The generated robots route contains the configured origin and crawl rules');

    const sitemap = await readRoute('/sitemap.xml', /^(?:application|text)\/xml\b/i);
    const parsed = await page.evaluate(xml => {
      const document = new DOMParser().parseFromString(xml, 'application/xml');
      return {
        errors: document.querySelectorAll('parsererror').length,
        root: document.documentElement.localName,
        namespace: document.documentElement.namespaceURI,
        locations: [...document.querySelectorAll('url > loc')].map(node => node.textContent),
      };
    }, sitemap);
    assert.equal(parsed.errors, 0);
    assert.equal(parsed.root, 'urlset');
    assert.equal(parsed.namespace, 'http://www.sitemaps.org/schemas/sitemap/0.9');
    for (const route of ['/', '/sign-in', '/courses', '/programs/game-ai-systems']) {
      assert.ok(parsed.locations.includes(route === '/' ? origin.origin : origin.origin + route), route);
    }
    assert.ok(parsed.locations.every(location => ['http:', 'https:'].includes(new URL(location).protocol)));
    passed('The sitemap advertised by robots is served as valid XML with the expected application routes');
    await writeFile(path.join(evidence, 'metadata-routes.json'), JSON.stringify({
      passed: true, cases, responses, mockedTransport: false,
      mode: 'Actual hosted Next development server and rendered application layout',
      productionBuildAcceptance: false, browserInstallationAccepted: false,
      scope: 'Generated metadata routes; personalized manifest and external catalog provider acceptance are not claimed.',
    }, null, 2));
  } catch (error) {
    await writeFile(path.join(evidence, 'metadata-routes.json'), JSON.stringify({
      passed: false, cases, responses, error: String(error), mockedTransport: false,
      productionBuildAcceptance: false, browserInstallationAccepted: false,
    }, null, 2));
    throw error;
  }
}
