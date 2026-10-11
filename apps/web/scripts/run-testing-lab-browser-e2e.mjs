import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { win32 } from 'node:path';
import { fileURLToPath } from 'node:url';

export function resolveBashExecutable({
  platform = process.platform,
  env = process.env,
  exists = existsSync,
} = {}) {
  if (env.TESTING_LAB_E2E_BASH) {
    return env.TESTING_LAB_E2E_BASH;
  }

  if (platform !== 'win32') {
    return 'bash';
  }

  const candidates = [];
  for (const root of [env.ProgramFiles, env['ProgramFiles(x86)']]) {
    if (root) {
      candidates.push(win32.join(root, 'Git', 'bin', 'bash.exe'));
    }
  }

  if (env.LOCALAPPDATA) {
    candidates.push(
      win32.join(env.LOCALAPPDATA, 'Programs', 'Git', 'bin', 'bash.exe'),
    );
  }

  for (const entry of (env.Path ?? env.PATH ?? '').split(';').filter(Boolean)) {
    const directoryName = win32.basename(entry).toLowerCase();
    // Windows exposes WSL relay/app aliases on PATH even when no Linux Bash
    // is installed. These cannot run this Windows checkout's Git Bash script.
    if (['system32', 'syswow64', 'windowsapps'].includes(directoryName)) {
      continue;
    }
    candidates.push(win32.join(entry, 'bash.exe'));
    if (directoryName === 'cmd') {
      candidates.push(win32.join(win32.dirname(entry), 'bin', 'bash.exe'));
    }
  }

  const bash = candidates.find((candidate) => exists(candidate));
  if (bash) {
    return bash;
  }

  throw new Error(
    'Git Bash was not found. Install Git for Windows or set TESTING_LAB_E2E_BASH to bash.exe.',
  );
}

export function runTestingLabBrowserE2E() {
  const bash = resolveBashExecutable();
  const script = fileURLToPath(
    new URL('./testing-lab-browser-e2e.sh', import.meta.url),
  );
  const child = spawn(bash, [script], {
    cwd: fileURLToPath(new URL('..', import.meta.url)),
    env: process.env,
    stdio: 'inherit',
    windowsHide: true,
  });

  child.once('error', (error) => {
    console.error(`[testing-lab-browser-e2e] failed to start: ${error.message}`);
    process.exitCode = 1;
  });
  child.once('exit', (code) => {
    process.exitCode = code ?? 1;
  });
}

if (
  process.argv[1] &&
  resolve(process.argv[1]) === resolve(fileURLToPath(import.meta.url))
) {
  runTestingLabBrowserE2E();
}
