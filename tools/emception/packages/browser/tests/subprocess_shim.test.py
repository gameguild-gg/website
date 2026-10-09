"""Exercise the real subprocess shim against an in-memory dispatch filesystem."""
import importlib.util
import io
import json
from pathlib import Path
import shlex
import unittest
from unittest.mock import patch


class MemoryFile(io.StringIO):
    def __init__(self, filesystem, path, mode):
        self.filesystem = filesystem
        self.path = path
        self.mode = mode
        super().__init__(filesystem.files.get(path, ""))
        if mode == "a":
            self.seek(0, io.SEEK_END)

    def close(self):
        if self.mode in ("w", "a") and not self.closed:
            self.filesystem.files[self.path] = self.getvalue()
        super().close()


class DispatchFilesystem:
    def __init__(self, returncode=0, stdout="compiler output\n", stderr="compiler error\n"):
        self.files = {"/tmp/.subprocess_stdin": "stale input"}
        self.opened = []
        self.requests = []
        self.returncode = returncode
        self.stdout = stdout
        self.stderr = stderr

    def open(self, path, mode):
        self.opened.append((path, mode))
        if path == "/tmp/__dispatch_subprocess__" and mode == "r":
            request = json.loads(self.files["/tmp/.subprocess_request"])
            request["stdin"] = self.files.get("/tmp/.subprocess_stdin")
            self.requests.append(request)
            self.files[path] = str(self.returncode)
            if self.stdout is not None:
                self.files["/tmp/.subprocess_stdout"] = self.stdout
            if self.stderr is not None:
                self.files["/tmp/.subprocess_stderr"] = self.stderr
        if mode == "r" and path not in self.files:
            raise FileNotFoundError(path)
        return MemoryFile(self, path, mode)

    def unlink(self, path):
        if path not in self.files:
            raise FileNotFoundError(path)
        del self.files[path]


class SubprocessShimTests(unittest.TestCase):
    def setUp(self):
        source = Path(__file__).resolve().parents[1] / "src/emscripten/subprocess_shim.py"
        specification = importlib.util.spec_from_file_location("emception_subprocess_shim", source)
        self.shim = importlib.util.module_from_spec(specification)
        specification.loader.exec_module(self.shim)

    def dispatch_with(self, filesystem, operation):
        with patch.object(self.shim, "open", filesystem.open, create=True), \
                patch.object(self.shim.os, "unlink", filesystem.unlink):
            return operation()

    def test_dispatch_does_not_persist_command_arguments_in_a_debug_log(self):
        filesystem = DispatchFilesystem()
        command = "clang --define=synthetic-private-argument source.c"
        result = self.dispatch_with(filesystem, lambda: self.shim._dispatch(command, "/workspace"))
        self.assertEqual(result, (0, "compiler output\n", "compiler error\n"))
        self.assertEqual(filesystem.requests, [{"cmd": command, "cwd": "/workspace", "stdin": None}])
        self.assertNotIn(("/tmp/subprocess_dispatch.log", "a"), filesystem.opened)
        self.assertEqual(filesystem.files, {})

    def test_run_preserves_argument_boundaries_input_and_binary_output(self):
        filesystem = DispatchFilesystem()
        arguments = ["clang", "file with spaces.c", "argument;with&metacharacters"]
        result = self.dispatch_with(filesystem, lambda: self.shim.run(
            arguments, capture_output=True, input=b"source input", cwd="/workspace"))
        self.assertEqual(shlex.split(filesystem.requests[0]["cmd"]), arguments)
        self.assertEqual(filesystem.requests[0]["stdin"], "source input")
        self.assertEqual(result.args, arguments)
        self.assertEqual(result.stdout, b"compiler output\n")
        self.assertEqual(result.stderr, b"compiler error\n")

    def test_checked_failure_preserves_status_and_requested_text_output(self):
        filesystem = DispatchFilesystem(returncode=7)
        with self.assertRaises(self.shim.CalledProcessError) as failure:
            self.dispatch_with(filesystem, lambda: self.shim.run(
                ["clang"], capture_output=True, text=True, check=True))
        self.assertEqual(failure.exception.returncode, 7)
        self.assertEqual(failure.exception.output, "compiler output\n")
        self.assertEqual(failure.exception.stderr, "compiler error\n")

    def test_missing_output_files_remain_optional(self):
        filesystem = DispatchFilesystem(stdout=None, stderr=None)
        result = self.dispatch_with(filesystem, lambda: self.shim.run(["clang"], capture_output=True))
        self.assertEqual(result.stdout, b"")
        self.assertEqual(result.stderr, b"")

    def test_popen_preserves_dispatch_result_and_cleanup(self):
        filesystem = DispatchFilesystem(returncode=2)
        process = self.dispatch_with(filesystem, lambda: self.shim.Popen(["clang", "source.c"]))
        self.assertEqual(process.communicate(), (b"compiler output\n", b"compiler error\n"))
        self.assertEqual(process.wait(), 2)
        self.assertEqual(process.poll(), 2)
        self.assertEqual(filesystem.files, {})


if __name__ == "__main__":
    unittest.main()
