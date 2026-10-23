# Generated metadata route acceptance — issues33,35,37

The current Next application has a manifest function, a robots function using the
configured public origin, and a sitemap function combining static routes with
program, project, course and blog sources. Root metadata links the manifest and
its SVG icon. Two existing manifest cases cover branding and launch/icon values;
four sitemap cases cover source aggregation, failure handling and the robots URL.
Those are unit cases; they do not prove emitted HTTP routes.

This prepared increment reuses the existing hosted Next/session acceptance server
and the already rendered application page. Four separate cases verify the root
manifest link, emitted manifest and icon, robots crawl rules and configured origin,
and the advertised sitemap's HTTP status, media type, XML and static/program URLs.
Requests go to the same disposable loopback server without mocked transport or
credentials. Existing seven session and four trust cases and SQL assertions stay
intact. A separate metadata-routes.json receipt is required by the workflow and
shares the existing checkout/tree/parents artifact identity.

This scopes 'generation' to Next's generated metadata routes. The original title
of issue33 does not specify user personalization or PWA installation; neither is
claimed by these checks. Sitemap unit cases use controlled data sources; real
external-provider completeness is not inferred from them. Hosted execution is
pending, and this prepared source does not recertify or close an issue.

No local SDK, dependency execution, server, browser, test or database was used.

## Pagination source correction for issue37

At the reviewed source, the sitemap calls getPublishedProjects once (the first24
projects) and getBlogIndex once (the first12 posts). It ignores the public blog
hasMore signal. The existing closure comment verifies generated routes and three
unit cases; that evidence did not exercise later pages. PR959 was inspected and
does not modify these sitemap/pagination sources.

The prepared helper follows project offset pages until a partial page and blog
PublishedAt/Id cursors until hasMore is false. It deduplicates stable identities
and rejects stalled or missing continuation data. Existing sitemap provider-failure
fallback and the conventional /sitemap.xml route remain intact. The API contracts,
public visibility filters and normal UI page sizes are unchanged.

Ten additional hosted unit cases cover later pages, overlapping projects, stalled
providers, absent blog index, invalid cursors and later-page failure. Four existing
sitemap composition cases remain. These are controlled-provider cases; actual
HTTP route acceptance is a separate four-case hosted receipt. None were executed
locally. No issue state or acceptance is changed by preparing this source.
