"""Pure artifact-contract tests; no product, database or real network execution."""

import copy
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import stat
import tempfile
import unittest
from unittest.mock import patch
import zipfile


SCRIPT = Path(__file__).resolve().parents[1] / "acquire-emception-release.py"
SPEC = importlib.util.spec_from_file_location("emception_release", SCRIPT)
release = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(release)
HEAD = "a" * 40
TREE = "b" * 40


class FakeGitHub:
    def __init__(self, run_status="in_progress", steps_passed=True):
        self.run_status = run_status
        self.steps_passed = steps_passed
        self.artifacts = []
        self.calls = []

    def json(self, endpoint):
        self.calls.append(endpoint)
        if endpoint.startswith("actions/workflows/"):
            return [{"workflow_runs": [{"id": 5, "head_sha": HEAD,
                      "path": ".github/workflows/emception.yml", "status": self.run_status}]}]
        if "/artifacts?" in endpoint:
            return [{"artifacts": self.artifacts}]
        if "/jobs?" in endpoint:
            names = ["Verify release gates and npm package contents",
                     "Prepare verified Toolchain CDN provenance", "Upload verified Toolchain CDN for Web"]
            return [{"jobs": [{"steps": [{"name": name,
                     "conclusion": "success" if self.steps_passed else None} for name in names]}]}]
        raise AssertionError("Unexpected endpoint: " + endpoint)


class FakeResponse(io.BytesIO):
    def __init__(self, data=b"{}", status=200, headers=None):
        super().__init__(data)
        self.status = status
        self.headers = headers or {}

    def getheader(self, name, default=None):
        return self.headers.get(name, default)


class FakeConnection:
    def __init__(self, response):
        self.response = response
        self.requests = []
        self.closed = False

    def request(self, method, path, headers):
        self.requests.append((method, path, headers))

    def getresponse(self):
        return self.response

    def close(self):
        self.closed = True


class ArtifactContractTests(unittest.TestCase):
    def setUp(self):
        base = Path(os.environ.get("CI_TEST_TEMP_DIRECTORY", tempfile.gettempdir())).resolve()
        self.temporary = tempfile.TemporaryDirectory(prefix="emception-artifact-contract-", dir=base)
        self.root = Path(self.temporary.name).resolve()
        self.assertTrue(self.root.is_relative_to(base))
        self.addCleanup(self.temporary.cleanup)

    def bundle(self, mutate=None, extra=None):
        files = {"cdn/manifest.json": b'{"schemaVersion":2,"artifactVersion":"4.4.1"}',
                 "cdn/compiler.wasm": b"verified compiler bytes"}
        provenance = {"schemaVersion": 1, "sourceHead": HEAD, "sourceTree": TREE,
                      "producerRunId": 5,
                      "manifestSha256": hashlib.sha256(files["cdn/manifest.json"]).hexdigest(),
                      "files": {name: {"bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
                                for name, data in files.items()}}
        if mutate:
            mutate(provenance)
        archive = self.root / "release.zip"
        with zipfile.ZipFile(archive, "w") as bundle:
            for name, data in files.items():
                bundle.writestr(name, data)
            bundle.writestr("provenance.json", json.dumps(provenance))
            if extra:
                bundle.writestr(*extra)
        artifact = {"id": 7, "name": release.artifact_name(HEAD, TREE), "expired": False,
                    "size_in_bytes": archive.stat().st_size,
                    "digest": "sha256:" + release.file_digest(archive),
                    "workflow_run": {"id": 5, "head_sha": HEAD}}
        return archive, artifact

    def test_accepts_exact_source_complete_verified_archive(self):
        archive, artifact = self.bundle()
        result = release.validate_archive(archive, artifact, HEAD, TREE)
        self.assertEqual(result["sourceTree"], TREE)
        self.assertEqual(len(result["files"]), 2)

    def test_rejects_different_head_tree_or_run(self):
        for field, wrong in [("sourceHead", "c" * 40), ("sourceTree", "c" * 40), ("producerRunId", 6)]:
            with self.subTest(field=field):
                archive, artifact = self.bundle(lambda value: value.update({field: wrong}))
                with self.assertRaisesRegex(ValueError, "source identity"):
                    release.validate_archive(archive, artifact, HEAD, TREE)

    def test_rejects_missing_inventory_and_file_digest_or_size_mismatch(self):
        mutations = [
            lambda value: value["files"].pop("cdn/compiler.wasm"),
            lambda value: value["files"]["cdn/compiler.wasm"].update({"sha256": "0" * 64}),
            lambda value: value["files"]["cdn/compiler.wasm"].update({"bytes": 1}),
            lambda value: value.update({"manifestSha256": "0" * 64}),
        ]
        for mutate in mutations:
            with self.subTest(mutation=mutate):
                archive, artifact = self.bundle(mutate)
                with self.assertRaises(ValueError):
                    release.validate_archive(archive, artifact, HEAD, TREE)

    def test_rejects_archive_digest_size_and_expansion_budget_mismatch(self):
        archive, artifact = self.bundle()
        for field, wrong in [("digest", "sha256:" + "0" * 64), ("size_in_bytes", 1),
                             ("size_in_bytes", release.MAX_ARCHIVE_BYTES + 1)]:
            with self.subTest(field=field, value=wrong):
                changed = copy.deepcopy(artifact)
                changed[field] = wrong
                with self.assertRaises(ValueError):
                    release.validate_archive(archive, changed, HEAD, TREE)
        with patch.object(release, "MAX_RELEASE_BYTES", 1):
            with self.assertRaisesRegex(ValueError, "expanded byte budget"):
                release.validate_archive(archive, artifact, HEAD, TREE)

    def test_rejects_traversal_absolute_backslash_uninventoried_and_duplicate_entries(self):
        for path in ["../outside", "/absolute", "cdn/../outside", "cdn\\escape", "C:/escape",
                     "unexpected", "cdn/extra.wasm", "cdn/compiler.wasm"]:
            with self.subTest(path=path):
                archive, artifact = self.bundle(extra=(path, b"outside inventory"))
                with self.assertRaises(ValueError):
                    release.validate_archive(archive, artifact, HEAD, TREE)

    def test_rejects_symlink_and_directory_entries(self):
        symlink = zipfile.ZipInfo("cdn/link")
        symlink.create_system = 3
        symlink.external_attr = (stat.S_IFLNK | 0o777) << 16
        for entry in [symlink, "cdn/directory/"]:
            with self.subTest(entry=entry):
                archive, artifact = self.bundle(extra=(entry, b"target"))
                with self.assertRaisesRegex(ValueError, "entry type"):
                    release.validate_archive(archive, artifact, HEAD, TREE)

    def test_selection_requires_all_three_successful_producer_steps(self):
        _, artifact = self.bundle()
        api = FakeGitHub(steps_passed=False)
        api.artifacts = [artifact]
        self.assertIsNone(release.choose_artifact(api, HEAD, TREE))
        api.steps_passed = True
        self.assertEqual(release.choose_artifact(api, HEAD, TREE)["id"], 7)

    def test_selection_rejects_wrong_run_ambiguous_and_unverified_terminal_producer(self):
        _, artifact = self.bundle()
        api = FakeGitHub()
        changed = copy.deepcopy(artifact)
        changed["workflow_run"]["id"] = 6
        api.artifacts = [changed]
        with self.assertRaisesRegex(ValueError, "producer identity"):
            release.choose_artifact(api, HEAD, TREE)
        api.artifacts = [artifact, artifact]
        with self.assertRaisesRegex(ValueError, "Ambiguous"):
            release.choose_artifact(api, HEAD, TREE)
        api.artifacts = []
        api.run_status = "completed"
        with self.assertRaisesRegex(ValueError, "completed without"):
            release.choose_artifact(api, HEAD, TREE)

    def test_bounded_wait_repolls_the_same_source_after_a_transient_timeout(self):
        _, artifact = self.bundle()
        clock = [0]
        calls = []

        def lookup(api, head, tree):
            calls.append((api, head, tree))
            if len(calls) == 1:
                raise TimeoutError("GitHub request timeout")
            if len(calls) == 2:
                return None
            return artifact

        def sleep(seconds):
            clock[0] += seconds

        api = FakeGitHub()
        with patch.object(release, "choose_artifact", side_effect=lookup):
            result = release.wait_for_artifact(api, HEAD, TREE, 90, lambda: clock[0], sleep)
        self.assertEqual(result["id"], 7)
        self.assertEqual(calls, [(api, HEAD, TREE)] * 3)

    def test_bounded_wait_preserves_missing_artifact_as_failure(self):
        clock = [0]

        def sleep(seconds):
            clock[0] += seconds

        with self.assertRaises(TimeoutError):
            release.wait_for_artifact(FakeGitHub(), HEAD, TREE, 60, lambda: clock[0], sleep)
        for bound in [0, -1, 19_801]:
            with self.subTest(bound=bound), self.assertRaises(ValueError):
                release.wait_for_artifact(FakeGitHub(), HEAD, TREE, bound)

    def test_scope_preserves_unrelated_web_builds_and_includes_producer_changes(self):
        self.assertFalse(release.needs_source_release(["apps/web/src/app/page.tsx", "docs/readme.md"]))
        for path in ["tools/emception/toolchain/overlays/curl.c", ".github/workflows/pr-verify.yml",
                     "scripts/ci/acquire-emception-release.py", "pnpm-lock.yaml",
                     "apps/web/scripts/coding-cycle-browser-e2e.mjs"]:
            with self.subTest(path=path):
                self.assertTrue(release.needs_source_release([path]))
        with self.assertRaises(ValueError):
            release.needs_source_release("not a list")

    def test_rejects_invalid_repository_and_source_identity(self):
        for repository in ["../outside", "owner/..", "owner/.", "owner/repo?token=secret", "https://example.test/repo"]:
            with self.subTest(repository=repository), self.assertRaises(ValueError):
                release.GitHub(repository)
        for source in ["latest", "a" * 39, "a" * 40 + "\n"]:
            with self.subTest(source=source), self.assertRaises(ValueError):
                release.artifact_name(source, TREE)

    def test_preparation_records_each_file_hash_and_exact_source(self):
        cdn = self.root / "tools/emception/artifacts/toolchain/release/cdn"
        cdn.mkdir(parents=True)
        (cdn / "manifest.json").write_text('{"schemaVersion":2}', encoding="utf8")
        (cdn / "compiler.wasm").write_bytes(b"compiler")
        with patch.object(release, "source_tree", return_value=TREE):
            name = release.prepare_provenance(self.root, HEAD, 5)
        self.assertEqual(name, release.artifact_name(HEAD, TREE))
        provenance = json.loads((cdn.parent / "provenance.json").read_text(encoding="utf8"))
        self.assertEqual(provenance["sourceHead"], HEAD)
        self.assertEqual(provenance["sourceTree"], TREE)
        self.assertEqual(provenance["files"]["cdn/compiler.wasm"]["bytes"], 8)

    def test_acquisition_installs_only_verified_files_into_the_new_release_directory(self):
        archive, artifact = self.bundle()
        api = FakeGitHub()

        def download(artifact_id, target):
            self.assertEqual(artifact_id, 7)
            target.write_bytes(archive.read_bytes())

        api.archive = download
        with patch.object(release, "source_tree", return_value=TREE), \
                patch.object(release, "wait_for_artifact", return_value=artifact):
            result = release.acquire(self.root, api, HEAD, 60)
        destination = self.root / "tools/emception/artifacts/toolchain/release"
        self.assertTrue(result["verified"])
        self.assertEqual((destination / "cdn/compiler.wasm").read_bytes(), b"verified compiler bytes")
        self.assertTrue((destination / "provenance.json").is_file())
        self.assertFalse(any(destination.parent.glob("emception-web-release-*")))

    def test_acquisition_rejects_an_existing_destination_without_modifying_it(self):
        destination = self.root / "tools/emception/artifacts/toolchain/release"
        destination.mkdir(parents=True)
        protected = destination / "protected.txt"
        protected.write_bytes(b"untouched")
        with patch.object(release, "source_tree", return_value=TREE), \
                patch.object(release, "wait_for_artifact") as lookup:
            with self.assertRaisesRegex(ValueError, "new directory"):
                release.acquire(self.root, FakeGitHub(), HEAD, 60)
        lookup.assert_not_called()
        self.assertEqual(protected.read_bytes(), b"untouched")

    def test_wait_retries_transient_api_errors_but_propagates_permission_failures(self):
        _, artifact = self.bundle()
        with patch.object(release, "choose_artifact", side_effect=[
            release.GitHubHTTPError(503), artifact,
        ]):
            self.assertEqual(release.wait_for_artifact(FakeGitHub(), HEAD, TREE, 60,
                             clock=lambda: 0, sleep=lambda seconds: self.assertEqual(seconds, 30))["id"], 7)
        with patch.object(release, "choose_artifact", side_effect=
                          release.GitHubHTTPError(403)):
            with self.assertRaises(release.GitHubHTTPError):
                release.wait_for_artifact(FakeGitHub(), HEAD, TREE, 60)

    def api(self):
        mock_credential = hashlib.sha256(b"HTTPS header verification fixture").hexdigest()
        return release.GitHub("gameguild-gg/gameguild", mock_credential)

    def test_checkout_tree_is_explicit_and_rejects_missing_or_invalid_identity(self):
        with patch.dict(os.environ, {"SOURCE_TREE_SHA": TREE}):
            self.assertEqual(release.source_tree(self.root), TREE)
        with patch.dict(os.environ, {"SOURCE_TREE_SHA": "latest"}):
            with self.assertRaises(ValueError):
                release.source_tree(self.root)
        with patch.dict(os.environ, {}, clear=True):
            with self.assertRaises(KeyError):
                release.source_tree(self.root)

    def test_authenticated_api_rejects_other_origins_and_path_escape_before_connecting(self):
        api = self.api()
        urls = ["http://api.github.com/repos/gameguild-gg/gameguild/actions/runs",
                "https://api.github.com.evil.test/repos/gameguild-gg/gameguild/actions/runs",
                "https://api.github.com/repos/other/repository/actions/runs",
                "https://api.github.com/repos/gameguild-gg/gameguild/actions/../issues",
                "https://api.github.com/repos/gameguild-gg/gameguild/actions/%2e%2e/issues",
                "https://api.github.com:444/repos/gameguild-gg/gameguild/actions/runs",
                "https://user@api.github.com/repos/gameguild-gg/gameguild/actions/runs",
                "https://api.github.com/repos/gameguild-gg/gameguild/actions/runs#fragment",
                "https://api.github.com/\r\nInjected: value"]
        with patch.object(release.http.client, "HTTPSConnection") as connect:
            for url in urls:
                with self.subTest(url=url), self.assertRaises(ValueError):
                    api.request(url, authenticated=True)
            connect.assert_not_called()

    def test_api_pagination_preserves_repository_origin_and_closes_connections(self):
        api = self.api()
        next_url = "https://api.github.com/repos/gameguild-gg/gameguild/actions/runs?page=2"
        first = FakeConnection(FakeResponse(b'{"workflow_runs":[{"id":1}]}',
                               headers={"Link": f'<{next_url}>; rel="next"'}))
        second = FakeConnection(FakeResponse(b'{"workflow_runs":[{"id":2}]}'))
        with patch.object(release.http.client, "HTTPSConnection", side_effect=[first, second]) as connect:
            pages = api.json("actions/runs?per_page=100")
        self.assertEqual([page["workflow_runs"][0]["id"] for page in pages], [1, 2])
        self.assertTrue(first.closed and second.closed)
        self.assertEqual([call.args[0] for call in connect.call_args_list], ["api.github.com"] * 2)
        self.assertEqual(first.requests[0][2]["Authorization"], "Bearer " + api.token)

    def test_api_rejects_off_origin_and_repeated_pagination(self):
        api = self.api()
        for next_url in ["https://evil.test/page", "https://api.github.com/repos/gameguild-gg/gameguild/actions/runs"]:
            with self.subTest(url=next_url):
                first = FakeConnection(FakeResponse(headers={"Link": f'<{next_url}>; rel="next"'}))
                with patch.object(release.http.client, "HTTPSConnection", return_value=first) as connect:
                    with self.assertRaises(ValueError):
                        api.json("actions/runs")
                    self.assertEqual(connect.call_count, 1)
                self.assertTrue(first.closed)

    def test_archive_redirect_does_not_forward_authentication_to_storage(self):
        api = self.api()
        storage_url = "https://results.blob.core.windows.net/archive.zip?signature=fixture"
        first = FakeConnection(FakeResponse(status=302, headers={"Location": storage_url}))
        second = FakeConnection(FakeResponse(b"ZIP fixture bytes", headers={"Content-Length": "17"}))
        target = self.root / "download.zip"
        with patch.object(release.http.client, "HTTPSConnection", side_effect=[first, second]) as connect:
            api.archive(7, target)
        self.assertEqual(target.read_bytes(), b"ZIP fixture bytes")
        self.assertIn("Authorization", first.requests[0][2])
        self.assertNotIn("Authorization", second.requests[0][2])
        self.assertEqual([call.args[0] for call in connect.call_args_list], ["api.github.com", "results.blob.core.windows.net"])
        self.assertTrue(first.closed and second.closed)

    def test_archive_rejects_untrusted_redirects_and_oversized_streams(self):
        api = self.api()
        for url in ["http://results.blob.core.windows.net/archive.zip", "file:///outside",
                    "https://evil.test/archive.zip", "https://blob.core.windows.net.evil.test/archive.zip"]:
            with self.subTest(url=url):
                first = FakeConnection(FakeResponse(status=302, headers={"Location": url}))
                with patch.object(release.http.client, "HTTPSConnection", return_value=first) as connect:
                    with self.assertRaises(ValueError):
                        api.archive(7, self.root / "invalid.zip")
                    self.assertEqual(connect.call_count, 1)
                self.assertTrue(first.closed)
        first = FakeConnection(FakeResponse(status=302, headers={"Location": "https://results.blob.core.windows.net/archive.zip"}))
        second = FakeConnection(FakeResponse(b"oversized fixture"))
        with patch.object(release.http.client, "HTTPSConnection", side_effect=[first, second]), \
                patch.object(release, "MAX_ARCHIVE_BYTES", 1):
            with self.assertRaisesRegex(ValueError, "byte or time budget"):
                api.archive(7, self.root / "oversized.zip")
        self.assertTrue(first.closed and second.closed)

    def test_api_error_classification_preserves_permission_failure(self):
        api = self.api()
        for status, headers, retryable in [(503, {}, True), (429, {}, True),
                                           (403, {"X-RateLimit-Remaining": "0"}, True),
                                           (403, {}, False), (401, {"X-RateLimit-Remaining": "0"}, False)]:
            with self.subTest(status=status, headers=headers):
                connection = FakeConnection(FakeResponse(status=status, headers=headers))
                with patch.object(release.http.client, "HTTPSConnection", return_value=connection):
                    with self.assertRaises(release.GitHubHTTPError) as error:
                        api.request("https://api.github.com/repos/gameguild-gg/gameguild/actions/runs", authenticated=True)
                self.assertEqual(error.exception.retryable, retryable)
                self.assertNotIn(api.token, str(error.exception))
                self.assertTrue(connection.closed)


if __name__ == "__main__":
    unittest.main()
