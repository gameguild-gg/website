"""Transfer a verified, exact-source Toolchain CDN between hosted CI workflows."""

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import tempfile
import time
import zipfile


MAX_ARCHIVE_BYTES = 512 * 1024 * 1024
MAX_RELEASE_BYTES = 2 * 1024 * 1024 * 1024
MAX_FILES = 20_000
SOURCE_SCOPE = re.compile(
    r"^(\.changeset/|\.github/workflows/(emception|pr-verify)\.yml|"
    r"scripts/ci/(acquire-emception-release\.py|tests/(acquire_emception_release_test\.py|emception-web-artifacts\.test\.mjs))|"
    r"apps/web/(Dockerfile|scripts/(sync-emception-cdn|coding-cycle-browser-e2e)\.mjs|src/lib/emception/)|"
    r"demos/emception-[^/]+/|package\.json$|pnpm-lock\.yaml$|pnpm-workspace\.yaml$|"
    r"scripts/devops/(emception-release-policy|publish-emception|version-emception)\.mjs|"
    r"scripts/sync-emception-cdn\.mjs|tools/emception/)"
)


def require_sha(value):
    if not isinstance(value, str) or re.fullmatch(r"[a-f0-9]{40}", value) is None:
        raise ValueError("Invalid source commit or tree identity")
    return value


def artifact_name(head, tree):
    return f"emception-web-{require_sha(head)}-{require_sha(tree)}"


def needs_source_release(paths):
    if not isinstance(paths, list) or any(not isinstance(path, str) for path in paths):
        raise ValueError("Changed paths must be a list of strings")
    return any(SOURCE_SCOPE.search(path.strip().replace("\\", "/")) for path in paths)


def sha256_stream(stream):
    digest = hashlib.sha256()
    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
        digest.update(chunk)
    return digest.hexdigest()


def file_digest(path):
    with path.open("rb") as stream:
        return sha256_stream(stream)


def source_tree(root):
    result = subprocess.run(
        ["git", "-c", "gc.auto=0", "-c", "maintenance.auto=false", "rev-parse", "HEAD^{tree}"],
        cwd=root, check=True, capture_output=True, text=True, timeout=30,
    )
    return require_sha(result.stdout.strip())


def prepare_provenance(root, head, run_id):
    head = require_sha(head)
    if not isinstance(run_id, int) or run_id <= 0:
        raise ValueError("Invalid producer run identity")
    tree = source_tree(root)
    release = root / "tools/emception/artifacts/toolchain/release"
    cdn = release / "cdn"
    if not cdn.resolve().is_relative_to(root.resolve()) or cdn.is_symlink():
        raise ValueError("CDN must remain inside the checkout")
    files = {}
    for directory, dirs, names in os.walk(cdn, followlinks=False):
        for name in [*dirs, *names]:
            if (Path(directory) / name).is_symlink():
                raise ValueError("CDN links are not supported")
        for name in names:
            path = Path(directory) / name
            files[path.relative_to(release).as_posix()] = {
                "bytes": path.stat().st_size, "sha256": file_digest(path),
            }
    if "cdn/manifest.json" not in files or len(files) > MAX_FILES:
        raise ValueError("Incomplete or oversized CDN release")
    if sum(entry["bytes"] for entry in files.values()) > MAX_RELEASE_BYTES:
        raise ValueError("CDN exceeds its byte budget")
    manifest = json.loads((cdn / "manifest.json").read_text(encoding="utf8"))
    if manifest.get("schemaVersion") != 2:
        raise ValueError("Verified release requires manifest schema 2")
    record = {
        "schemaVersion": 1, "sourceHead": head, "sourceTree": tree,
        "producerRunId": run_id,
        "manifestSha256": files["cdn/manifest.json"]["sha256"],
        "files": dict(sorted(files.items())),
    }
    (release / "provenance.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf8")
    return artifact_name(head, tree)


class GitHub:
    def __init__(self, repository):
        if (re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9_.-]{1,100}", repository) is None or
                repository.rsplit("/", 1)[1] in {".", ".."}):
            raise ValueError("Invalid repository identity")
        self.prefix = f"repos/{repository}/"

    def json(self, endpoint):
        result = subprocess.run(
            ["gh", "api", self.prefix + endpoint, "--paginate", "--slurp"],
            check=True, capture_output=True, timeout=90,
        )
        return json.loads(result.stdout)

    def archive(self, artifact_id, target):
        if not isinstance(artifact_id, int) or artifact_id <= 0:
            raise ValueError("Invalid artifact identity")
        with target.open("xb") as stream:
            subprocess.run(
                ["gh", "api", f"{self.prefix}actions/artifacts/{artifact_id}/zip"],
                check=True, stdout=stream, stderr=subprocess.PIPE, timeout=180,
            )


def choose_artifact(api, head, tree):
    name = artifact_name(head, tree)
    pages = api.json(f"actions/workflows/emception.yml/runs?head_sha={head}&per_page=100")
    runs = sorted(
        [run for page in pages for run in page["workflow_runs"] if run["head_sha"] == head],
        key=lambda run: (run["id"], run.get("run_attempt", 1)), reverse=True,
    )
    for run in runs:
        if run.get("path") != ".github/workflows/emception.yml":
            raise ValueError("Unexpected producer workflow")
        artifacts = [a for page in api.json(f"actions/runs/{run['id']}/artifacts?per_page=100")
                     for a in page["artifacts"] if a["name"] == name and not a["expired"]]
        if len(artifacts) > 1:
            raise ValueError("Ambiguous producer artifact")
        if artifacts:
            artifact = artifacts[0]
            if artifact["workflow_run"]["id"] != run["id"] or artifact["workflow_run"]["head_sha"] != head:
                raise ValueError("Artifact producer identity mismatch")
            jobs = [job for page in api.json(f"actions/runs/{run['id']}/jobs?filter=latest&per_page=100")
                    for job in page["jobs"]]
            required_steps = {
                "Verify release gates and npm package contents",
                "Prepare verified Toolchain CDN provenance",
                "Upload verified Toolchain CDN for Web",
            }
            if any(required_steps.issubset({step["name"] for step in job["steps"] if step["conclusion"] == "success"})
                   for job in jobs):
                return artifact
    if runs and all(run["status"] == "completed" for run in runs):
        raise ValueError("Producer completed without a verified artifact for this source tree")
    return None


def wait_for_artifact(api, head, tree, wait_seconds, clock=time.monotonic, sleep=time.sleep):
    if not 0 < wait_seconds <= 19_800:
        raise ValueError("Artifact wait must be bounded to at most 330 minutes")
    deadline = clock() + wait_seconds
    while True:
        try:
            artifact = choose_artifact(api, head, tree)
        except subprocess.TimeoutExpired:
            artifact = None
        except subprocess.CalledProcessError as error:
            detail = (error.stderr or b"").decode("utf8", errors="replace")
            if not re.search(r"HTTP (429|5\d\d)|rate limit", detail, re.IGNORECASE):
                raise
            artifact = None
        if artifact is not None:
            return artifact
        remaining = deadline - clock()
        if remaining <= 0:
            raise TimeoutError("Verified producer release is still unavailable")
        print("Waiting for the exact-source verified Toolchain release", flush=True)
        sleep(min(30, remaining))


def validate_archive(archive, artifact, head, tree):
    if not 0 < artifact.get("size_in_bytes", 0) <= MAX_ARCHIVE_BYTES:
        raise ValueError("Artifact exceeds its archive budget")
    if archive.stat().st_size != artifact["size_in_bytes"]:
        raise ValueError("Artifact byte count mismatch")
    if artifact.get("digest") != "sha256:" + file_digest(archive):
        raise ValueError("Artifact digest mismatch")
    with zipfile.ZipFile(archive) as bundle:
        entries = bundle.infolist()
        names = [entry.filename for entry in entries]
        if len(entries) > MAX_FILES + 1 or len(set(names)) != len(names):
            raise ValueError("Duplicate or oversized archive entries")
        if sum(entry.file_size for entry in entries) > MAX_RELEASE_BYTES:
            raise ValueError("Release exceeds its expanded byte budget")
        for entry in entries:
            path = PurePosixPath(entry.filename)
            if (path.is_absolute() or ".." in path.parts or "\\" in entry.filename or
                    ":" in entry.filename or str(path) != entry.filename or entry.is_dir() or
                    not (entry.filename == "provenance.json" or entry.filename.startswith("cdn/")) or
                    stat.S_ISLNK(entry.external_attr >> 16) or entry.flag_bits & 1):
                raise ValueError("Unsupported archive path or entry type")
        if "provenance.json" not in names or "cdn/manifest.json" not in names:
            raise ValueError("Missing release provenance or manifest")
        if bundle.getinfo("provenance.json").file_size > 16 * 1024 * 1024:
            raise ValueError("Oversized release provenance")
        provenance = json.loads(bundle.read("provenance.json"))
        if (provenance.get("schemaVersion") != 1 or provenance.get("sourceHead") != head or
                provenance.get("sourceTree") != tree or
                provenance.get("producerRunId") != artifact["workflow_run"]["id"]):
            raise ValueError("Frozen release source identity mismatch")
        files = provenance.get("files")
        if not isinstance(files, dict) or set(files) != set(names) - {"provenance.json"}:
            raise ValueError("Provenance file inventory mismatch")
        if provenance.get("manifestSha256") != files["cdn/manifest.json"].get("sha256"):
            raise ValueError("Manifest provenance mismatch")
        if bundle.testzip() is not None:
            raise ValueError("Artifact ZIP CRC mismatch")
        for name, record in files.items():
            if not isinstance(record, dict) or record.get("bytes") != bundle.getinfo(name).file_size:
                raise ValueError("Release file byte count mismatch")
            with bundle.open(name) as stream:
                if record.get("sha256") != sha256_stream(stream):
                    raise ValueError("Release file digest mismatch")
        return provenance


def acquire(root, api, head, wait_seconds):
    head = require_sha(head)
    tree = source_tree(root)
    destination = root / "tools/emception/artifacts/toolchain/release"
    parent = destination.parent.resolve()
    if not parent.is_relative_to(root.resolve()) or destination.is_symlink() or destination.exists():
        raise ValueError("Release destination must be a new directory inside the checkout")
    artifact = wait_for_artifact(api, head, tree, wait_seconds)
    if not 0 < artifact.get("size_in_bytes", 0) <= MAX_ARCHIVE_BYTES:
        raise ValueError("Artifact exceeds its archive budget")
    parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="emception-web-release-", dir=parent) as directory:
        temporary = Path(directory).resolve()
        if not temporary.is_relative_to(parent):
            raise ValueError("Release staging escaped its directory")
        archive = temporary / "release.zip"
        api.archive(artifact["id"], archive)
        provenance = validate_archive(archive, artifact, head, tree)
        payload = temporary / "payload"
        payload.mkdir()
        with zipfile.ZipFile(archive) as bundle:
            for entry in bundle.infolist():
                target = payload / entry.filename
                if not target.resolve().is_relative_to(payload):
                    raise ValueError("Release output escaped its directory")
                target.parent.mkdir(parents=True, exist_ok=True)
                with bundle.open(entry) as source, target.open("xb") as output:
                    shutil.copyfileobj(source, output)
        payload.rename(destination)
    return {
        "artifactId": artifact["id"], "producerRunId": provenance["producerRunId"],
        "sourceHead": head, "sourceTree": tree, "archiveDigest": artifact["digest"],
        "fileCount": len(provenance["files"]), "verified": True,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prepare", action="store_true")
    parser.add_argument("--wait-seconds", type=int, default=19_800)
    options = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    head = os.environ["SOURCE_HEAD_SHA"]
    if options.prepare:
        name = prepare_provenance(root, head, int(os.environ["GITHUB_RUN_ID"]))
        with Path(os.environ["GITHUB_OUTPUT"]).open("a", encoding="utf8") as output:
            output.write(f"artifact-name={name}\n")
        print(json.dumps({"artifactName": name, "sourceHead": head}))
    else:
        required = needs_source_release(json.loads(os.environ["CHANGED_PATHS"]))
        with Path(os.environ["GITHUB_OUTPUT"]).open("a", encoding="utf8") as output:
            output.write(f"required={str(required).lower()}\n")
        if required:
            print(json.dumps(acquire(root, GitHub(os.environ["GITHUB_REPOSITORY"]), head, options.wait_seconds)))
        else:
            print("Toolchain source unchanged; using the existing pinned release build path")


if __name__ == "__main__":
    main()
