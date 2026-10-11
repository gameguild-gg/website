"""Pure artifact-contract tests; no product, database or real network execution."""

import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import stat
import subprocess
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
                raise subprocess.TimeoutExpired("gh", 90)
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
            subprocess.CalledProcessError(1, "gh", stderr=b"HTTP 503"), artifact,
        ]):
            self.assertEqual(release.wait_for_artifact(FakeGitHub(), HEAD, TREE, 60,
                             clock=lambda: 0, sleep=lambda seconds: self.assertEqual(seconds, 30))["id"], 7)
        with patch.object(release, "choose_artifact", side_effect=
                          subprocess.CalledProcessError(1, "gh", stderr=b"HTTP 403 permission denied")):
            with self.assertRaises(subprocess.CalledProcessError):
                release.wait_for_artifact(FakeGitHub(), HEAD, TREE, 60)


if __name__ == "__main__":
    unittest.main()
