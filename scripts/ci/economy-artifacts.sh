#!/usr/bin/env bash
set -Eeuo pipefail

# Transfer only outputs from a successful job in this workflow run. A bundle
# from another source tree, a damaged archive or an unsafe entry fails closed.
operation="${1:?pack or unpack}" kind="${2:?api, client or web-tests}"
source="${3:?source directory}" destination="${4:?destination directory}"
case "$operation:$kind" in pack:api|pack:client|pack:web-tests|unpack:api|unpack:client|unpack:web-tests) ;; *) exit 2 ;; esac
tree="$(git --no-optional-locks -c gc.auto=0 -c maintenance.auto=false rev-parse 'HEAD^{tree}')"
commit="$(git --no-optional-locks -c gc.auto=0 -c maintenance.auto=false rev-parse HEAD)"
python3 - "$operation" "$kind" "$source" "$destination" "$tree" "$commit" <<'PY'
import hashlib
import json
import pathlib
import shutil
import sys
import tarfile

operation, kind, source, destination, tree, commit = sys.argv[1:]
source, destination = pathlib.Path(source).resolve(), pathlib.Path(destination).resolve()
destination.mkdir(parents=True, exist_ok=True)

def digest(path):
    value = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()

if operation == "pack":
    if not source.is_dir() or not any(source.iterdir()):
        raise SystemExit("Empty Economy output bundle")
    for path in source.rglob("*"):
        if not path.resolve().is_relative_to(source):
            raise SystemExit(f"Output link escapes its bundle: {path}")
    archive = destination / "outputs.tar.gz"
    with tarfile.open(archive, "w:gz", dereference=True) as handle:
        handle.add(source, arcname="outputs")
    identity = dict(kind=kind, tree=tree, commit=commit, sha256=digest(archive))
    (destination / "identity.json").write_text(json.dumps(identity) + "\n", encoding="utf-8")
else:
    identity = json.loads((source / "identity.json").read_text(encoding="utf-8"))
    archive = source / "outputs.tar.gz"
    if identity["kind"] != kind or identity["tree"] != tree or identity["sha256"] != digest(archive):
        raise SystemExit("Economy bundle kind, source tree or SHA256 mismatch")
    with tarfile.open(archive, "r:gz") as handle:
        members = handle.getmembers()
        for member in members:
            name = pathlib.PurePosixPath(member.name)
            if name.is_absolute() or ".." in name.parts or not name.parts or name.parts[0] != "outputs":
                raise SystemExit(f"Unsafe Economy bundle path: {member.name}")
            if not (member.isdir() or member.isfile()):
                raise SystemExit(f"Unsupported Economy bundle entry: {member.name}")
        for member in members:
            relative = pathlib.PurePosixPath(member.name).relative_to("outputs")
            target = destination / relative
            if not target.resolve().is_relative_to(destination) or target.is_symlink():
                raise SystemExit(f"Destination escapes its bundle: {target}")
            if member.isdir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with handle.extractfile(member) as incoming, target.open("wb") as outgoing:
                    shutil.copyfileobj(incoming, outgoing)
                target.chmod(0o755 if member.mode & 0o111 else 0o644)
    print(f"Verified {kind} bundle from tree {tree}: {identity['sha256']}")
PY
