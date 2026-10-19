# Economy Web test placement

The existing client producer runs the complete Web Vitest suite and Economy Web
coverage after building the client. Those commands already unset `API_URL` and do
not need the published API or its PostgreSQL database. The producer runs in
parallel with API publication, on its own runner. Web tests and the Next build do
not compete for memory on a single runner.

The producer keeps the existing commands, worker count, test filters and 100%
coverage thresholds. A separate `web-tests` bundle holds both Vitest reports and
the coverage directory. The browser gate downloads this through the existing
same-run, same-attempt client artifact. It validates the bundle kind, source tree,
SHA-256 and safe archive entries with the existing transfer helper, then rejects
missing, empty, failed or pending suites and incomplete coverage before using it.
The identity is retained in the browser evidence. Missing or invalid evidence
fails the gate; it does not fall back to an unverified success.

The full/release profiles still execute the same Web tests locally within their
gate. OpenAPI verification, Next build, both Playwright suites, .NET coverage,
all regression shards, test assertions and job deadlines are unchanged.

## Observation motivating the change

On 2026-10-18, source c4e09a38's hosted gate completed successfully in 918 seconds.
Its source-bound timing artifact records 283 seconds for the Web suite and
18 seconds for Economy Web coverage, followed by a 334-second Next build. The
client producer took 216 seconds while API publication took 426 seconds. Moving
Web tests into that existing producer can overlap part of their execution with
API publication. This is a timing hypothesis, not a measured improvement for the
new source. Producer overhead and artifact transfer must also be measured.

The six additional shell-contract groups run only in hosted CI under the local
resource hold. They cover valid evidence, missing reports, failed/pending suites,
coverage gaps, transfer identity/integrity and required routing. No SDK, product
tests, fixtures, databases or containers were run locally for this change.
