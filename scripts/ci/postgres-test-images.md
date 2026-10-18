# PostgreSQL images for hosted CI

PR Verify and Emception use the original digest-pinned Docker Official PostgreSQL images through Google's `mirror.gcr.io` cache. On 2026-10-17, ECR returned `toomanyrequests: Data limit exceeded` in required jobs on PR #869. Hosted CI now uses the independent cache while preserving both complete manifest digests and PostgreSQL major versions. Failed pulls still fail the gate; there is no retry fallback that changes the database version.

## Image identity

On 2026-10-09, public registry manifest reads returned byte-identical multi-platform indexes from Docker Hub and ECR for each tag. The SHA-256 of each response body matched the digest below. No database or container was started to make this comparison.

| Tag | Manifest index digest |
| --- | --- |
| `postgres:17-alpine` | `sha256:b0f9560a2de083e2cc7382e75f808c7381a32852a7ec49117deedb300e552b24` |
| `postgres:16-alpine` | `sha256:721873c34ceb9f8d8fc265984940dc982404c105f19ad51be9fdc5970a6080ea` |

AWS documents the `public.ecr.aws/docker/library` namespace as [Docker Official Images published to ECR Public](https://aws.amazon.com/blogs/containers/docker-official-images-now-available-on-amazon-elastic-container-registry-public/).

On 2026-10-17, direct HTTPS manifest reads from `mirror.gcr.io/v2/library/postgres/manifests/<digest>` returned HTTP 200 for both original indexes. Each response contained 10,301 bytes, and its SHA-256 matched the original digest in the table. No image, database or container was downloaded or started for this comparison. Google documents the [managed Docker Hub cache](https://docs.cloud.google.com/artifact-registry/docs/pull-cached-dockerhub-images). Cache availability is not guaranteed indefinitely; the actual hosted gates must verify acquisition and test execution.

## Configuration

- `GAMEGUILD_TEST_POSTGRES_17_IMAGE`: API and Economy test runners and the real coding assessment browser cycle.
- `GAMEGUILD_TEST_POSTGRES_16_IMAGE`: the Testing Lab browser runner.
- Migration and OpenAPI job services use the same pinned PostgreSQL 17 URI literally, because services are created before steps run.

Both workflows set the image variables explicitly. Main commit `1ae0f9d87` (PR #876) also moved the manual API and Economy PostgreSQL 17 defaults and Testing Lab's PostgreSQL 16 default to `mirror.gcr.io/library`. Those upstream defaults are preserved here. The Code browser runner retains its previous Docker Hub tag for manual runs. Hosted workflows override these defaults with the pinned digests. Container ownership, passwords, readiness probes, storage, migration and cleanup rules are unchanged. Production Compose files and unrelated Testcontainers images are outside this configuration.

## Updating a pin

1. Keep the existing PostgreSQL major version for each runner.
2. Read the official manifest index and the selected registry/cache response for that major tag; compare the complete response bytes and verify their SHA-256 digests. Confirm the required Linux platforms are present.
3. Update both workflow image variables, both service literals and the expected digests in `scripts/ci/tests/postgres-images.test.mjs` together.
4. Run the offline source contract and require the actual hosted API, Economy, migration, OpenAPI, Testing Lab and Code gates to accept the new head. A passing source contract alone does not prove that the image can be pulled or that database tests pass.
