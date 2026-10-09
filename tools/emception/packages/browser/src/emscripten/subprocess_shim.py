"""
Python subprocess shim for Emscripten driver integration.

When emcc.py calls subprocess.run() or subprocess.Popen, this shim
intercepts the call and routes it through the ToolRunner instead of
spawning a real process (impossible in the browser).

Communication protocol:
  1. Write JSON request to /tmp/.subprocess_request
  2. Open /tmp/__dispatch_subprocess__ for reading — this triggers the
     patched ___syscall_openat in python.mjs which calls
     Module["subprocessDispatch"]() via Asyncify.handleAsync
  3. subprocessDispatch runs the tool and writes results to /tmp/
  4. Read exit code from /tmp/__dispatch_subprocess__
  5. Read stdout/stderr from /tmp/.subprocess_stdout and /tmp/.subprocess_stderr
  6. Remove /tmp/__dispatch_subprocess__ so next dispatch re-triggers the hook

This file is injected into CPython's module path as `subprocess.py`,
replacing the standard library's subprocess module.
"""

import os
import json
import shlex

class CompletedProcess:
    def __init__(self, args, returncode, stdout=b'', stderr=b''):
        self.args = args
        self.returncode = returncode
        self.stdout = stdout
        self.stderr = stderr

DEVNULL = -3
PIPE = -1
STDOUT = -2

def _dispatch(cmd_str, cwd=None, input_data=None):
    """Dispatch a subprocess via file-based IPC + os.system JSPI bridge."""
    request = json.dumps({'cmd': cmd_str, 'cwd': cwd or ''})
    with open('/tmp/.subprocess_request', 'w') as f:
        f.write(request)

    # Write input data (stdin) if provided
    if input_data is not None:
        stdin_str = input_data if isinstance(input_data, str) else input_data.decode('utf-8', errors='replace')
        with open('/tmp/.subprocess_stdin', 'w') as f:
            f.write(stdin_str)
    else:
        # Remove any stale stdin file
        try:
            os.unlink('/tmp/.subprocess_stdin')
        except (FileNotFoundError, OSError):
            pass

    # Trigger dispatch via file open — uses __syscall_openat → Asyncify.
    # os.system() uses __emscripten_system which is NOT properly Asyncify-
    # instrumented through CPython's indirect call dispatch (tp_call etc.),
    # causing 'unreachable' WASM traps. File open goes through __syscall_openat
    # which IS in ASYNCIFY_IMPORTS and works reliably.
    # The patched ___syscall_openat in python.mjs intercepts this path,
    # calls Module["subprocessDispatch"]() via Asyncify.handleAsync,
    # which runs the tool and writes results to /tmp/.
    with open('/tmp/__dispatch_subprocess__', 'r') as f:
        rc = int(f.read().strip() or '1')
    # Remove trigger file so next dispatch re-triggers the async onPreOpen hook
    # (otherwise isCachedSync finds stale data and skips the hook)
    try:
        os.unlink('/tmp/__dispatch_subprocess__')
    except (FileNotFoundError, OSError):
        pass

    stdout = ''
    stderr = ''
    try:
        with open('/tmp/.subprocess_stdout', 'r') as f:
            stdout = f.read()
    except (FileNotFoundError, OSError):
        pass
    try:
        with open('/tmp/.subprocess_stderr', 'r') as f:
            stderr = f.read()
    except (FileNotFoundError, OSError):
        pass

    # Clean up IPC files
    for p in ['/tmp/.subprocess_request', '/tmp/.subprocess_stdout', '/tmp/.subprocess_stderr', '/tmp/.subprocess_stdin']:
        try:
            os.unlink(p)
        except (FileNotFoundError, OSError):
            pass

    return rc, stdout, stderr

def run(args, capture_output=False, text=False, cwd=None, env=None,
        check=False, input=None, timeout=None, **kwargs):
    stdout_mode = kwargs.get('stdout', None)
    stderr_mode = kwargs.get('stderr', None)

    if isinstance(args, str):
        cmd = args
    else:
        cmd = shlex.join(args) if hasattr(shlex, 'join') else ' '.join(str(a) for a in args)

    rc, out, err = _dispatch(cmd, cwd, input_data=input)

    if text:
        stdout = out
        stderr = err
    else:
        stdout = out.encode('utf-8', errors='replace') if isinstance(out, str) else out
        stderr = err.encode('utf-8', errors='replace') if isinstance(err, str) else err

    # If stdout/stderr were not requested (not PIPE), don't capture
    if not capture_output and stdout_mode != PIPE:
        stdout = b'' if not text else ''
    if not capture_output and stderr_mode != PIPE:
        stderr = b'' if not text else ''

    result = CompletedProcess(args, rc, stdout, stderr)
    if check and rc != 0:
        raise CalledProcessError(rc, args, stdout, stderr)
    return result

def call(args, **kwargs):
    return run(args, **kwargs).returncode

def check_call(args, **kwargs):
    return run(args, check=True, **kwargs).returncode

def check_output(args, **kwargs):
    return run(args, capture_output=True, check=True, **kwargs).stdout

class Popen:
    def __init__(self, args, stdin=None, stdout=None, stderr=None,
                 cwd=None, env=None, **kwargs):
        self.args = args
        if isinstance(args, str):
            cmd = args
        else:
            cmd = shlex.join(args) if hasattr(shlex, 'join') else ' '.join(str(a) for a in args)
        rc, out, err = _dispatch(cmd, cwd)
        self.returncode = rc
        if isinstance(out, str):
            self.stdout = out.encode('utf-8', errors='replace')
        else:
            self.stdout = out
        if isinstance(err, str):
            self.stderr = err.encode('utf-8', errors='replace')
        else:
            self.stderr = err
        self.pid = 0

    def communicate(self, input=None, timeout=None):
        return self.stdout, self.stderr

    def wait(self, timeout=None):
        return self.returncode

    def poll(self):
        return self.returncode

    def kill(self):
        pass

    def terminate(self):
        pass

    def __enter__(self):
        return self

    def __exit__(self, *args):
        pass

class SubprocessError(Exception):
    pass

class CalledProcessError(SubprocessError):
    def __init__(self, returncode, cmd, output=None, stderr=None):
        self.returncode = returncode
        self.cmd = cmd
        self.output = output
        self.stderr = stderr

class TimeoutExpired(SubprocessError):
    def __init__(self, cmd, timeout, output=None, stderr=None):
        self.cmd = cmd
        self.timeout = timeout
        self.output = output
        self.stderr = stderr
