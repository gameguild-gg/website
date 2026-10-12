import type { BrowserEmceptionAPI, BrowserStdin, CanvasToolchain, NativePreset } from '@gameguild/emception-browser';
import { TOOLCHAIN_PRESETS as EMCEPTION_PRESETS, createEmception } from '@gameguild/emception-browser';
import type { OnMount } from '@monaco-editor/react';
import { Terminal } from '@xterm/xterm';
import { ToolchainPreset } from 'emception';
import { Fragment, useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Group, Panel, Separator } from 'react-resizable-panels';
import DockGroupPanel from './DockGroup.js';
import FileExplorer from './FileExplorer.js';
import type { DockGroup, IdeController, IdeExtension, IdeProps, OpenTab, TerminalTab, WorkspaceConfig, WorkspaceFile } from './ide-types.js';
import { activateIdeExtensions, validateIdeExtensions } from './ide-extensions.js';
import {
  DEFAULT_IMAGE,
  mergeRestoredWorkspaceFiles,
  parseWorkspaceBundle,
  resolveArgs,
  resolveWorkspaceStorageKey,
  shouldPersistWorkspace,
  workspaceFilesForStorage,
  workspaceConfigToState,
} from './ide-types.js';
import { buildFileTree, inferLanguage, isSourceFile, isTextFile, resolveWsPath } from './ide-utils.js';
import TerminalPanel from './TerminalPanel.js';
import { DEFAULT_PRESET, PRESETS, PRESET_IDS } from './workspace-presets.js';

interface IdeTerminal {
  clear(): void;
  dispose(): void;
  readByteExclusive(): number | Promise<number>;
  write(text: string): void;
  writeError(text: string): void;
  writeLine(text: string): void;
}

interface IdeRunOptions {
  cwd?: string;
  env?: Record<string, string>;
  hints?: { bundlesNeeded?: string[] };
  stdin?: BrowserStdin;
  onStdout?: (text: string) => void;
  onStderr?: (text: string) => void;
}

function createIdeTerminal(terminal: Terminal, onClear: () => void): IdeTerminal {
  const bytes: number[] = [];
  const waiters: Array<(byte: number) => void> = [];
  const subscription = terminal.onData((data) => {
    for (let index = 0; index < data.length; index += 1) {
      const byte = data.charCodeAt(index);
      const waiter = waiters.shift();
      if (waiter) waiter(byte);
      else bytes.push(byte);
    }
  });
  return {
    clear: () => {
      terminal.clear();
      onClear();
    },
    dispose: () => subscription.dispose(),
    readByteExclusive: () => bytes.shift() ?? new Promise<number>((resolve) => waiters.push(resolve)),
    write: (text) => terminal.write(text),
    writeError: (text) => terminal.writeln(`\x1b[31m${text}\x1b[0m`),
    writeLine: (text) => terminal.writeln(text),
  };
}

async function runTool(api: BrowserEmceptionAPI, tool: string, argv: string[], options: IdeRunOptions = {}) {
  const stdoutDecoder = new TextDecoder();
  const stderrDecoder = new TextDecoder();
  return api.run(tool, argv, {
    cwd: options.cwd,
    env: options.env,
    stdin: options.stdin,
    preloadBundles: options.hints?.bundlesNeeded,
    stdout: options.onStdout ? (chunk) => options.onStdout?.(stdoutDecoder.decode(chunk, { stream: true })) : 'capture',
    stderr: options.onStderr ? (chunk) => options.onStderr?.(stderrDecoder.decode(chunk, { stream: true })) : 'capture',
  });
}

function resolveCanvasToolchain(toolchain: WorkspaceConfig['compile']['toolchain']): CanvasToolchain | null {
  switch (toolchain) {
    case ToolchainPreset.SDL_CPP:
    case ToolchainPreset.SDL_C:
    case ToolchainPreset.Raylib_CPP:
    case ToolchainPreset.Raylib_C:
    case ToolchainPreset.Allegro_CPP:
    case ToolchainPreset.Allegro_C:
      return toolchain;
    default:
      return null;
  }
}

/** Creates a line-buffered stdin reader from the tty. Shared by WASI, CMake, and Python paths. */
function makeLineBufferedStdin(tty: { readByteExclusive: () => number | Promise<number> | null }): () => Promise<number> {
  const lineQueue: number[] = [];
  let lineBuf = '';
  let lineCursor = 0;
  return async (): Promise<number> => {
    if (lineQueue.length > 0) return lineQueue.shift()!;
    while (true) {
      const raw = tty.readByteExclusive();
      if (raw === null) {
        await new Promise<void>((resolve) => setTimeout(resolve, 8));
        continue;
      }
      const byte: number = typeof (raw as Promise<number>).then === 'function' ? await (raw as Promise<number>) : (raw as number);
      if (byte === -1 || byte === 0) {
        await new Promise<void>((resolve) => setTimeout(resolve, 8));
        continue;
      }
      if (byte === 127 || byte === 8) {
        if (lineCursor > 0) {
          lineBuf = lineBuf.slice(0, lineCursor - 1) + lineBuf.slice(lineCursor);
          lineCursor--;
        }
        continue;
      }
      if (byte === 13 || byte === 10) {
        // Ignore empty/whitespace-only CR/LF lines to avoid delivering stale
        // terminal newlines as immediate blank stdin records.
        if (lineBuf.trim().length === 0) {
          lineBuf = '';
          lineCursor = 0;
          continue;
        }
        for (let i = 0; i < lineBuf.length; i++) lineQueue.push(lineBuf.charCodeAt(i));
        lineQueue.push(10);
        lineBuf = '';
        lineCursor = 0;
        return lineQueue.shift()!;
      }
      if (byte >= 32) {
        const ch = String.fromCharCode(byte);
        lineBuf = lineBuf.slice(0, lineCursor) + ch + lineBuf.slice(lineCursor);
        lineCursor++;
      }
    }
  };
}

export type { IdeProps };

export default function Ide({
  title = 'Emception',
  manifestUrl,
  api: injectedApi,
  onReady,
  onDispose,
  extensions,
  workspaceConfig,
  workspaceUrl,
  workspaceName,
  workspaceStorageKey,
  enableFileExplorer = true,
  enableTabs = true,
  enableTerminal = true,
  enableCanvas = true,
  enableDocking = true,
  enableWorkspace = true,
  allowFileCreation = true,
  fullscreen = false,
  onFullscreenChange,
  showHiddenFiles = false,
  showSolutionFiles = false,
  onStdout,
  onStderr,
  stdin: stdinProp,
  readOnly = false,
  theme = 'vs-dark',
}: IdeProps) {
  const activeExtensions = useMemo(() => validateIdeExtensions(extensions), [extensions]);
  const storageKey = resolveWorkspaceStorageKey(workspaceName, workspaceStorageKey);
  const editorRef = useRef<Parameters<OnMount>[0] | null>(null);
  const monacoRef = useRef<Parameters<OnMount>[1] | null>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  /** Hidden holder div that keeps the <canvas> alive across dock group moves */
  const canvasHolderRef = useRef<HTMLDivElement | null>(null);
  /** The div inside whichever DockGroupPanel currently shows the canvas tab */
  const canvasHostElRef = useRef<HTMLDivElement | null>(null);
  const apiRef = useRef<BrowserEmceptionAPI | null>(null);
  const ownsApiRef = useRef(false);
  const terminalIORef = useRef<IdeTerminal | null>(null);
  const xtermRef = useRef<Terminal | null>(null);
  const terminalLogRef = useRef<HTMLPreElement | null>(null);

  // Resolve the active workspace config: prop > fetched bundle > default preset
  const [activePresetId, setActivePresetId] = useState<string>(workspaceConfig?.id ?? DEFAULT_PRESET.id);
  const [fetchedConfig, setFetchedConfig] = useState<WorkspaceConfig | null>(null);
  const resolvedConfig = workspaceConfig ?? fetchedConfig ?? PRESETS[activePresetId] ?? DEFAULT_PRESET;
  const initialState = workspaceConfigToState(resolvedConfig);

  const [files, setFiles] = useState<Record<string, WorkspaceFile>>(initialState.files);
  const [selectedPath, setSelectedPath] = useState(initialState.activeTabId.startsWith('tab:') ? initialState.activeTabId.slice(4) : '');
  const [expandedDirs, setExpandedDirs] = useState<Set<string>>(initialState.expandedDirs);
  const [openTabs, setOpenTabs] = useState<OpenTab[]>(initialState.openTabs);
  const [activeTabId, setActiveTabId] = useState(initialState.activeTabId);
  const [canvasIsRunning, setCanvasIsRunning] = useState(false);
  const [workspaceRestored, setWorkspaceRestored] = useState(false);

  const [terminalTabs, setTerminalTabs] = useState<TerminalTab[]>([{ id: 'terminal-1', title: 'bash' }]);
  const [activeTerminalId, setActiveTerminalId] = useState('terminal-1');

  const [status, setStatus] = useState('Initializing...');
  const [isReady, setIsReady] = useState(false);
  const [publishedController, setPublishedController] = useState<IdeController | null>(null);
  const [terminalReady, setTerminalReady] = useState(false);
  const [executionPhase, setExecutionPhase] = useState<'idle' | 'compiling' | 'running'>('idle');
  /** Set to true by handleStop so the catch block in handleCompile knows it was intentional */
  const stoppedRef = useRef(false);
  /** Tracks latest files for use in callbacks that can't close over state */
  const filesRef = useRef(files);
  filesRef.current = files;
  // Expose filesRef for e2e tests so Playwright can verify file content was updated
  (window as unknown as Record<string, unknown>).__emception_filesRef__ = filesRef;

  // Visibility filter: honour showHiddenFiles and showSolutionFiles props.
  const visiblePaths = Object.keys(files).filter((path) => {
    const name = path.split('/').pop() ?? '';
    if (!showHiddenFiles && name.startsWith('.')) return false;
    if (!showSolutionFiles && /\.solution\./.test(name)) return false;
    return true;
  });
  const fileTree = buildFileTree(visiblePaths);
  const activeTab = openTabs.find((t) => t.id === activeTabId) ?? openTabs[0] ?? null;
  const activeFile = activeTab && activeTab.type !== 'canvas' ? (files[activeTab.path] ?? null) : null;
  const activeFileName = activeTab?.type === 'canvas' ? 'Canvas' : (activeFile?.path.split('/').filter(Boolean).pop() ?? '');
  const groupTabs = (group: DockGroup) => openTabs.filter((t) => t.group === group);
  const hasRightGroup = groupTabs('right').length > 0;
  const hasBottomGroup = groupTabs('bottom').length > 0;

  useEffect(() => {
    try {
      const raw = enableWorkspace ? window.localStorage.getItem(storageKey) : null;
      if (!raw) return;
      const parsed = JSON.parse(raw) as {
        files?: Record<string, WorkspaceFile>;
        selectedPath?: string;
        expandedDirs?: string[];
        openTabs?: OpenTab[];
        activeTabId?: string;
      };
      if (parsed.files && Object.keys(parsed.files).length > 0) {
        const nextFiles = mergeRestoredWorkspaceFiles(initialState.files, parsed.files);
        setFiles(nextFiles);
      }
      if (parsed.selectedPath) setSelectedPath(parsed.selectedPath);
      if (parsed.expandedDirs) setExpandedDirs(new Set(parsed.expandedDirs));
      if (parsed.openTabs && parsed.openTabs.length > 0) setOpenTabs(parsed.openTabs);
      if (parsed.activeTabId) setActiveTabId(parsed.activeTabId);
    } catch {
      /* ignore storage read errors */
    } finally {
      setWorkspaceRestored(true);
    }
  }, []);

  useEffect(() => {
    try {
      if (!shouldPersistWorkspace(enableWorkspace, workspaceRestored)) return;
      // Persist workspace files
      const filesToSave = workspaceFilesForStorage(files);
      window.localStorage.setItem(
        storageKey,
        JSON.stringify({
          files: filesToSave,
          selectedPath,
          expandedDirs: [...expandedDirs],
          openTabs,
          activeTabId,
        }),
      );
    } catch {
      /* ignore storage write errors */
    }
  }, [files, selectedPath, expandedDirs, openTabs, activeTabId, enableWorkspace, workspaceRestored, storageKey]);

  // ── Fetch workspace from URL ──────────────────────────────────
  useEffect(() => {
    if (!workspaceUrl) return;
    let cancelled = false;
    (async () => {
      try {
        const resp = await fetch(workspaceUrl);
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
        const text = await resp.text();
        const config = parseWorkspaceBundle(text);
        if (cancelled) return;
        setFetchedConfig(config);
        const state = workspaceConfigToState(config);
        setFiles(state.files);
        setOpenTabs(state.openTabs);
        setActiveTabId(state.activeTabId);
        setExpandedDirs(state.expandedDirs);
        setSelectedPath(state.activeTabId.startsWith('tab:') ? state.activeTabId.slice(4) : '');
      } catch (e) {
        console.error('[Emception:IDE] Failed to fetch workspace bundle:', e);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [workspaceUrl]);

  // ── Sync workspace files into the Worker VFS (/home/user) ─────
  const syncFilesToVfs = useCallback(async (filesToSync: Record<string, WorkspaceFile>) => {
    const api = apiRef.current;
    if (!api) return;
    const P = '[Emception:IDE]';
    const enc = new TextEncoder();
    const textFiles = Object.values(filesToSync).filter((f) => f.type === 'text' && isTextFile(f.path));
    for (const file of textFiles) {
      await api.workspace.writeFile(file.path, enc.encode(file.content));
      console.log(`${P} VFS sync: ${file.path}`);
    }
    console.log(`${P} VFS sync complete (${textFiles.length} files)`);
  }, []);

  const syncWorkspace = useCallback(
    () => syncFilesToVfs(filesRef.current),
    [syncFilesToVfs],
  );

  const replaceFiles = useCallback(async (nextFiles: readonly WorkspaceFile[]) => {
    const next = Object.fromEntries(nextFiles.map((file) => [file.path, { ...file }])) as Record<string, WorkspaceFile>;
    const api = apiRef.current;
    if (api) {
      for (const previousFile of Object.values(filesRef.current)) {
        if (previousFile.type !== 'text' || next[previousFile.path]) continue;
        if (await api.workspace.readFile(previousFile.path) !== null) {
          await api.workspace.deleteFile(previousFile.path);
        }
      }
      await syncFilesToVfs(next);
    }
    filesRef.current = next;
    setFiles(next);
    const nextOpenTabs: OpenTab[] = Object.values(next).map((file) => ({
      id: `tab:${file.path}`,
      path: file.path,
      type: file.type,
      group: 'main',
    }));
    setOpenTabs(nextOpenTabs);
    const nextPath = nextFiles[0]?.path ?? '';
    setSelectedPath(nextPath);
    setActiveTabId(nextPath ? `tab:${nextPath}` : '');
  }, [syncFilesToVfs]);

  const setFilesReadOnly = useCallback((paths: readonly string[], nextReadOnly: boolean) => {
    const pathSet = new Set(paths);
    setFiles((previous) => {
      const next = { ...previous };
      for (const path of pathSet) {
        const file = next[path];
        if (file) next[path] = { ...file, readonly: nextReadOnly };
      }
      filesRef.current = next;
      return next;
    });
  }, []);

  const publishController = useCallback((api: BrowserEmceptionAPI) => {
    const controller: IdeController = {
      api,
      getFiles: async () => Object.values(filesRef.current).map((file) => ({ ...file })),
      syncWorkspace,
      replaceFiles,
      setFilesReadOnly,
    };
    setPublishedController(controller);
  }, [replaceFiles, setFilesReadOnly, syncWorkspace]);

  useEffect(() => {
    if (!publishedController) return;
    const disposeExtensions = activateIdeExtensions(activeExtensions, publishedController);
    onReady?.(publishedController);
    return () => {
      try {
        disposeExtensions();
      } finally {
        onDispose?.();
      }
    };
  }, [activeExtensions, onDispose, onReady, publishedController]);

  // ── Switch workspace preset ───────────────────────────────────
  const switchWorkspace = useCallback(
    async (presetId: string) => {
      const preset = PRESETS[presetId];
      if (!preset) return;
      const P = '[Emception:IDE]';
      console.log(`${P} ===== WORKSPACE SWITCH: "${activePresetId}" → "${presetId}" =====`);

      apiRef.current?.canvas.stop();
      // Reset canvas state
      const canvas = canvasRef.current;
      if (canvas) {
        delete canvas.dataset.sdlRunning;
        canvas.style.display = 'none';
      }
      setCanvasIsRunning(false);
      setExecutionPhase('idle');
      stoppedRef.current = true;

      // Reset VFS in the Worker to clear stale build artifacts from the previous workspace
      if (apiRef.current) {
        const tty = terminalIORef.current;
        tty?.clear();
        tty?.writeLine(`\x1b[33mSwitching workspace...\x1b[0m`);
        try {
          console.log(`${P} Resetting Worker VFS (clearing /tmp and /home/user)...`);
          await apiRef.current.workspace.reset();
          console.log(`${P} Worker VFS reset complete`);
        } catch (err) {
          console.warn(`${P} VFS reset failed, continuing:`, err);
        }
      }

      stoppedRef.current = false;
      setActivePresetId(presetId);
      const state = workspaceConfigToState(preset);
      setFiles(state.files);
      setOpenTabs(state.openTabs);
      setActiveTabId(state.activeTabId);
      setExpandedDirs(state.expandedDirs);
      setSelectedPath(state.activeTabId.startsWith('tab:') ? state.activeTabId.slice(4) : '');

      // Dispose stale Monaco models from the OLD workspace after React re-renders.
      // We defer disposal so @monaco-editor/react can cleanly unmount its model
      // reference first (the DockGroupPanel remounts via key={activePresetId}).
      const mc = monacoRef.current;
      if (mc) {
        const oldModels = mc.editor.getModels().slice();
        queueMicrotask(() => {
          for (const m of oldModels) {
            try {
              m.dispose();
            } catch {
              /* already disposed */
            }
          }
        });
      }

      if (apiRef.current) {
        terminalIORef.current?.writeLine(`\x1b[32mSwitched to workspace: ${preset.label}\x1b[0m`);
        // Sync new workspace files into VFS so /home/user is populated immediately
        await syncFilesToVfs(state.files);
      }
      console.log(`${P} ===== WORKSPACE SWITCH COMPLETE =====`);
    },
    [activePresetId, syncFilesToVfs],
  );

  const handleBootTerminalReady = useCallback((term: Terminal) => {
    xtermRef.current = term;
    terminalIORef.current?.dispose();
    terminalIORef.current = createIdeTerminal(term, () => {
      if (terminalLogRef.current) terminalLogRef.current.textContent = '';
    });
    (window as Window & { __xterm__?: Terminal }).__xterm__ = term;
    term.writeln('\x1b[32mWelcome to the Browser C/C++ Toolchain!\x1b[0m');
    term.writeln('Booting system...');
    setTerminalReady(true);
  }, []);

  const doBootstrap = useCallback(
    async (isMounted: () => boolean = () => true) => {
      const xterm = xtermRef.current;
      if (!xterm) return;
      setIsReady(false);
      setStatus('Booting toolchain...');
      try {
        // Mirror ALL xterm output to a hidden DOM element for Playwright E2E tests.
        // Must be patched BEFORE createEmception so the MiniShell banner (sent by the
        // Worker before the 'booted' reply) is also captured in the log.
        // eslint-disable-next-line no-control-regex
        const stripAnsi = (s: string) => s.replace(/\u001b\[[\d;]*m/g, '');
        const log = terminalLogRef;
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const xtermAny = xterm as any;
        if (!xtermAny.__emceptionLogPatched) {
          const origXtermWriteln = xtermAny.writeln.bind(xterm);
          xtermAny.writeln = (data: string | Uint8Array, callback?: () => void) => {
            origXtermWriteln(data, callback);
            if (log.current && typeof data === 'string') log.current.textContent += stripAnsi(data) + '\n';
          };
          const origXtermWrite = xtermAny.write.bind(xterm);
          xtermAny.write = (data: string | Uint8Array, callback?: () => void) => {
            origXtermWrite(data, callback);
            if (log.current && typeof data === 'string') log.current.textContent += stripAnsi(data);
          };
          xtermAny.__emceptionLogPatched = true;
        }

        const result = injectedApi ?? await createEmception({ manifestUrl, container: xterm, tty: 'xterm' });
        const ownsApi = !injectedApi;
        if (!isMounted()) {
          if (ownsApi) result.dispose();
          return;
        }
        apiRef.current = result;
        ownsApiRef.current = ownsApi;
        (window as unknown as Record<string, unknown>).__emception_api__ = result;
        // Sync current workspace files into VFS so /home/user is populated on boot
        await syncFilesToVfs(filesRef.current);
        if (!isMounted()) {
          if (ownsApi) result.dispose();
          return;
        }
        publishController(result);
        setStatus('Ready');
        setIsReady(true);
        xterm.writeln('\x1b[32mSystem Ready.\x1b[0m');
        xterm.writeln('Click \x1b[1m▶\x1b[0m to compile & run.');
      } catch (err) {
        if (!isMounted()) return;
        setStatus(`Error: ${err instanceof Error ? err.message : String(err)}`);
        xterm.writeln(`\x1b[31mBoot failed: ${err}\x1b[0m`);
      }
    },
    [injectedApi, manifestUrl, publishController, syncFilesToVfs],
  );

  useEffect(() => {
    if (!terminalReady || !xtermRef.current) return;
    let mounted = true;
    doBootstrap(() => mounted);
    return () => {
      mounted = false;
      apiRef.current?.canvas.stop();
      if (ownsApiRef.current) apiRef.current?.dispose();
      apiRef.current = null;
      setPublishedController(null);
      ownsApiRef.current = false;
    };
  }, [terminalReady, manifestUrl, doBootstrap]);

  useEffect(() => {
    return () => {
      terminalIORef.current?.dispose();
      terminalIORef.current = null;
    };
  }, []);

  const ensureOpenTab = useCallback(
    (path: string, group: DockGroup = 'main') => {
      const file = files[path];
      if (!file) return;
      const id = `tab:${path}`;
      setOpenTabs((prev) => {
        const existing = prev.find((t) => t.id === id);
        if (existing) return prev.map((t) => (t.id === id ? ({ ...t, group } as OpenTab) : t));
        return [...prev, { id, path, type: file.type, group }];
      });
      setActiveTabId(id);
    },
    [files],
  );

  const ensureCanvasTab = useCallback((group: DockGroup = 'right') => {
    setOpenTabs((prev) => {
      const existing = prev.find((t) => t.id === 'canvas');
      if (existing) return prev.map((t) => (t.id === 'canvas' ? ({ ...t, group } as OpenTab) : t));
      return [...prev, { id: 'canvas', type: 'canvas', group }];
    });
    setActiveTabId('canvas');
  }, []);

  const closeTab = useCallback((tabId: string) => {
    setOpenTabs((prev) => {
      const idx = prev.findIndex((t) => t.id === tabId);
      if (idx === -1) return prev;
      const next = prev.filter((t) => t.id !== tabId);
      setActiveTabId((cur) => {
        if (cur !== tabId) return cur;
        return next[idx]?.id ?? next[idx - 1]?.id ?? next[0]?.id ?? '';
      });
      return next;
    });
  }, []);

  const moveTabToGroup = useCallback((tabId: string, group: DockGroup) => {
    setOpenTabs((prev) => prev.map((t) => (t.id === tabId ? ({ ...t, group } as OpenTab) : t)));
    setActiveTabId(tabId);
  }, []);

  /** Reorder a tab by placing it just before `beforeTabId` in the openTabs array. */
  const reorderTab = useCallback((tabId: string, beforeTabId: string) => {
    setOpenTabs((prev) => {
      const srcIdx = prev.findIndex((t) => t.id === tabId);
      const dstIdx = prev.findIndex((t) => t.id === beforeTabId);
      if (srcIdx === -1 || dstIdx === -1 || srcIdx === dstIdx) return prev;
      const tab = { ...prev[srcIdx], group: prev[dstIdx].group } as OpenTab;
      const next = prev.filter((_, i) => i !== srcIdx);
      const insertIdx = next.findIndex((t) => t.id === beforeTabId);
      next.splice(insertIdx, 0, tab);
      return next;
    });
    setActiveTabId(tabId);
  }, []);

  /** Called by DockGroupPanel when a canvas host div mounts/unmounts.
   *  Reparents the persistent <canvas> element into the new host. */
  const handleCanvasHost = useCallback((el: HTMLDivElement | null) => {
    canvasHostElRef.current = el;
    const canvas = canvasRef.current;
    if (!canvas) return;
    if (el) {
      // Move the canvas into the new host panel
      el.prepend(canvas);
      canvas.style.display = canvas.dataset.sdlRunning ? 'block' : 'none';
    } else {
      // Host unmounted — park canvas back in the hidden holder
      canvasHolderRef.current?.appendChild(canvas);
    }
  }, []);

  // Safety net: if handleCanvasHost fired before canvasRef was set (rare timing
  // edge on initial mount), reparent once both refs are available.
  useLayoutEffect(() => {
    const host = canvasHostElRef.current;
    const canvas = canvasRef.current;
    if (host && canvas && canvas.parentElement !== host) {
      host.prepend(canvas);
      canvas.style.display = canvas.dataset.sdlRunning ? 'block' : 'none';
    }
  });

  const createFile = useCallback(
    (kind: 'text' | 'image') => {
      if (readOnly || !allowFileCreation) return;
      const baseDir = '/user';
      const defaultName = kind === 'image' ? 'new-image.svg' : 'new-file.cpp';
      const input = window.prompt(`Create new ${kind} file`, `${baseDir}/${defaultName}`);
      if (!input) return;
      const path = input.startsWith('/') ? input : `${baseDir}/${input}`;
      if (files[path]) {
        window.alert(`File already exists: ${path}`);
        return;
      }
      const content = kind === 'image' ? DEFAULT_IMAGE : kind === 'text' && path.endsWith('.h') ? '#pragma once\n\n' : '// New source file\n';
      setFiles((prev) => ({ ...prev, [path]: { path, type: kind, content } }));
      const parts = path.split('/').filter(Boolean);
      if (parts.length > 1) {
        setExpandedDirs((prev) => {
          const next = new Set(prev);
          let cur = '';
          for (let i = 0; i < parts.length - 1; i++) {
            cur += `/${parts[i]}`;
            next.add(cur);
          }
          return next;
        });
      }
      setSelectedPath(path);
      ensureOpenTab(path, 'main');
    },
    [files, ensureOpenTab, readOnly, allowFileCreation],
  );

  const renameSelectedFile = useCallback(() => {
    if (readOnly || !selectedPath || !files[selectedPath] || files[selectedPath].readonly) return;
    const nextPath = window.prompt('Rename file', selectedPath);
    if (!nextPath || nextPath === selectedPath) return;
    if (files[nextPath]) {
      window.alert(`File already exists: ${nextPath}`);
      return;
    }
    const norm = nextPath.startsWith('/') ? nextPath : `/${nextPath}`;
    const file = files[selectedPath];
    setFiles((prev) => {
      const clone = { ...prev };
      delete clone[selectedPath];
      clone[norm] = { ...file, path: norm };
      return clone;
    });
    setOpenTabs((prev) =>
      prev.map((tab) => (tab.type !== 'canvas' && tab.path === selectedPath ? ({ ...tab, path: norm, id: `tab:${norm}` } as OpenTab) : tab)),
    );
    setSelectedPath(norm);
    setActiveTabId((cur) => (cur === `tab:${selectedPath}` ? `tab:${norm}` : cur));
  }, [selectedPath, files, readOnly]);

  const deleteSelectedFile = useCallback(() => {
    if (readOnly || !selectedPath || !files[selectedPath] || files[selectedPath].readonly) return;
    if (!window.confirm(`Delete ${selectedPath}?`)) return;
    setFiles((prev) => {
      const c = { ...prev };
      delete c[selectedPath];
      return c;
    });
    closeTab(`tab:${selectedPath}`);
    setSelectedPath(Object.keys(files).find((p) => p !== selectedPath) ?? '');
  }, [selectedPath, files, closeTab, readOnly]);

  const resetWorkspace = useCallback(async () => {
    if (!window.confirm('Reset the workspace to the default demo files and layout?')) return;
    const P = '[Emception:IDE]';
    console.log(`${P} ===== WORKSPACE RESET =====`);
    apiRef.current?.canvas.stop();
    // Reset canvas state
    const resetCanvas = canvasRef.current;
    if (resetCanvas) {
      delete resetCanvas.dataset.sdlRunning;
      resetCanvas.style.display = 'none';
    }
    setCanvasIsRunning(false);
    setExecutionPhase('idle');
    stoppedRef.current = true;

    // Reset VFS in the Worker to clear stale build artifacts
    if (apiRef.current) {
      try {
        console.log(`${P} Resetting Worker VFS...`);
        await apiRef.current.workspace.reset();
        console.log(`${P} Worker VFS reset complete`);
      } catch (err) {
        console.warn(`${P} VFS reset failed, continuing:`, err);
      }
    }

    stoppedRef.current = false;
    const state = workspaceConfigToState(resolvedConfig);
    setFiles(state.files);
    setSelectedPath(state.activeTabId.startsWith('tab:') ? state.activeTabId.slice(4) : '');
    setExpandedDirs(state.expandedDirs);
    setOpenTabs(state.openTabs);
    setActiveTabId(state.activeTabId);
    setTerminalTabs([{ id: 'terminal-1', title: 'bash' }]);
    setActiveTerminalId('terminal-1');
    if (apiRef.current) {
      terminalIORef.current?.clear();
      terminalIORef.current?.writeLine('\x1b[32mWorkspace reset.\x1b[0m');
    } else {
      xtermRef.current?.clear();
      xtermRef.current?.writeln('\x1b[32mWorkspace reset.\x1b[0m');
    }
  }, [resolvedConfig]);

  const createTerminalTab = useCallback(() => {
    setTerminalTabs((prev) => {
      const nextId = `terminal-${prev.length + 1}`;
      const next = [...prev, { id: nextId, title: `bash ${prev.length + 1}` }];
      setActiveTerminalId(nextId);
      return next;
    });
  }, []);

  const closeTerminalTab = useCallback((tabId: string) => {
    setTerminalTabs((prev) => {
      if (prev.length === 1) return prev;
      const idx = prev.findIndex((t) => t.id === tabId);
      if (idx === -1) return prev;
      const next = prev.filter((t) => t.id !== tabId);
      setActiveTerminalId((cur) => {
        if (cur !== tabId) return cur;
        return (next[idx] ?? next[idx - 1] ?? next[0])?.id ?? 'terminal-1';
      });
      return next;
    });
  }, []);

  const handleEditorDidMount: OnMount = (editor, monaco) => {
    editorRef.current = editor;
    monacoRef.current = monaco;
    // Expose for e2e tests (e.g. Playwright can read file content via window.monaco)
    (window as unknown as Record<string, unknown>).monaco = monaco;
    syncMonacoModels();
  };

  const handleEditorChange = useCallback((path: string, value: string) => {
    if (readOnly || filesRef.current[path]?.readonly) return;
    setFiles((prev) => {
      const next = { ...prev, [path]: { ...prev[path], content: value } };
      // Update ref immediately so handleCompile (which reads filesRef.current)
      // always sees the latest content even if React hasn't re-rendered yet.
      filesRef.current = next;
      return next;
    });
  }, [readOnly]);

  const syncMonacoModels = useCallback(() => {
    const monaco = monacoRef.current;
    if (!monaco) return;

    const desiredPaths = new Set<string>();

    const ensureModel = (path: string) => {
      const file = filesRef.current[path];
      if (!file || file.type !== 'text') return;

      desiredPaths.add(path);
      const uri = monaco.Uri.file(path);
      const existing = monaco.editor.getModel(uri);

      if (!existing) {
        monaco.editor.createModel(file.content, inferLanguage(path), uri);
        return;
      }

      if (existing.getValue() !== file.content && activeTabId !== `tab:${path}`) {
        existing.setValue(file.content);
      }
    };

    for (const tab of openTabs) {
      if (tab.type !== 'canvas') ensureModel(tab.path);
    }
    if (selectedPath) ensureModel(selectedPath);

    const activePath = activeTabId.startsWith('tab:') ? activeTabId.slice(4) : null;
    for (const model of monaco.editor.getModels()) {
      if (model.uri.scheme !== 'file') continue;
      const modelPath = model.uri.path;
      if (!desiredPaths.has(modelPath) && modelPath !== activePath) {
        model.dispose();
      }
    }
  }, [activeTabId, openTabs, selectedPath]);

  useEffect(() => {
    syncMonacoModels();
  }, [files, syncMonacoModels]);

  // Expose for e2e tests so Playwright can update file content directly in React state
  (window as unknown as Record<string, unknown>).__setFileContent = handleEditorChange;

  const teardownSdlRuntime = useCallback(() => {
    const canvas = canvasRef.current;
    const wasRunning = canvas?.dataset.sdlRunning === 'true';
    apiRef.current?.canvas.stop();
    if (canvas) {
      canvas.getContext('2d')?.clearRect(0, 0, canvas.width, canvas.height);
      delete canvas.dataset.sdlRunning;
      canvas.style.display = 'none';
    }

    setCanvasIsRunning(false);
    return wasRunning;
  }, []);

  const handleCompile = async () => {
    const api = apiRef.current;
    const tty = terminalIORef.current;
    if (!api || !tty || !activeFile || activeFile.type !== 'text') return;
    stoppedRef.current = false;
    setExecutionPhase('compiling');
    setActiveTerminalId('terminal-1');
    const P = '[Emception:IDE]';
    // Always return keyboard focus to the editor when done, regardless of exit path.
    // xterm captures document-level keydown when it has focus, blocking Monaco input.
    const restoreEditorFocus = () => {
      editorRef.current?.focus();
    };
    const tTotal = performance.now();
    // Read from filesRef.current (updated immediately by handleEditorChange)
    // instead of the render-time `files` closure, so e2e __setFileContent
    // updates are always picked up even before React re-renders.
    const currentFiles = filesRef.current;

    if (resolvedConfig.run.type === 'canvas' && executionPhase === 'running') {
      teardownSdlRuntime();
    }

    const textFiles = Object.values(currentFiles).filter((f) => f.type === 'text' && isTextFile(f.path));

    // Determine which source file to compile/run
    const cwd = resolvedConfig.compile.cwd ?? `/home/user/${resolvedConfig.id}`;
    const entryPointRel = resolvedConfig.compile.sourceDetect?.entryPoint;
    const entryPoint = entryPointRel ? resolveWsPath(cwd, entryPointRel) : undefined;
    const compileTarget = isSourceFile(activeFile.path)
      ? activeFile.path
      : entryPoint && currentFiles[entryPoint]
        ? entryPoint
        : textFiles.find((f) => isSourceFile(f.path))?.path;

    try {
      // ── Sync all text files to VFS ──────────────────────────────
      tty.clear();
      console.log(`${P} COMPILE & RUN START`);
      const enc = new TextEncoder();
      for (const file of textFiles) {
        await api.workspace.writeFile(file.path, enc.encode(file.content));
        console.log(`${P} Synced ${file.path}`);
      }

      const t0 = performance.now();
      const runType = resolvedConfig.run.type;

      // ── Python script path ──────────────────────────────────────
      if (runType === 'python-script') {
        const pyFile = compileTarget ?? entryPoint ?? `${cwd}/main.py`;
        const args = resolvedConfig.run.args ? resolveArgs(resolvedConfig.run.args, pyFile) : ['python3', pyFile];
        setStatus('Running Python...');
        tty.writeLine(`\x1b[36mRunning ${pyFile}...\x1b[0m`);
        setExecutionPhase('running');
        const lineBufferedStdin = makeLineBufferedStdin(tty);
        await runTool(api, args[0], args, {
          cwd: resolvedConfig.compile.cwd ?? `/home/user/${resolvedConfig.id}`,
          onStdout: (t: string) => {
            tty.write(t.replace(/\n/g, '\r\n'));
          },
          onStderr: (t: string) => {
            tty.write(`\x1b[31m${t.replace(/\n/g, '\r\n')}\x1b[0m`);
          },
          stdin: lineBufferedStdin,
        });
        setExecutionPhase('idle');
        setStatus(`Done (${((performance.now() - tTotal) / 1000).toFixed(1)}s)`);
        return;
      }

      // ── CMake build path ────────────────────────────────────────
      if (runType === 'cmake-build') {
        if (!compileTarget && !entryPoint) {
          setExecutionPhase('idle');
          setStatus('No source file found');
          tty.writeError('No source file found in workspace.');
          return;
        }
        setStatus('CMake configure...');
        tty.writeLine('\x1b[36mCMake configure...\x1b[0m');
        const configArgs = resolvedConfig.compile.args;
        const configResult = await runTool(api, configArgs[0], configArgs, {
          cwd: resolvedConfig.compile.cwd ?? `/home/user/${resolvedConfig.id}`,
          onStdout: (t: string) => {
            tty.writeLine(t);
          },
          onStderr: (t: string) => {
            tty.writeError(t);
          },
        });
        if (configResult.exitCode !== 0) {
          setExecutionPhase('idle');
          setStatus('CMake configure failed');
          tty.writeLine(`\x1b[31mCMake configure failed (exit ${configResult.exitCode})\x1b[0m`);
          return;
        }
        setStatus('Ninja build...');
        tty.writeLine('\x1b[36mNinja build...\x1b[0m');
        const buildDir = configArgs.includes('-B') ? configArgs[configArgs.indexOf('-B') + 1] : '/home/user/build';
        const ninjaResult = await runTool(api, 'ninja', ['ninja', '-C', buildDir], {
          cwd: resolvedConfig.compile.cwd ?? `/home/user/${resolvedConfig.id}`,
          onStdout: (t: string) => {
            tty.writeLine(t);
          },
          onStderr: (t: string) => {
            tty.writeError(t);
          },
        });
        const duration = ((performance.now() - t0) / 1000).toFixed(2);
        if (ninjaResult.exitCode !== 0) {
          setExecutionPhase('idle');
          setStatus(`Build failed (${duration}s)`);
          tty.writeLine(`\x1b[31mNinja build failed (exit ${ninjaResult.exitCode})\x1b[0m`);
          return;
        }
        tty.writeLine(`\x1b[32mBuild successful in ${duration}s\x1b[0m`);
        tty.writeLine('Running...');
        const runArgs = resolvedConfig.run.args ?? ['wasi-run', resolvedConfig.compile.output];
        setExecutionPhase('running');
        const lineBufferedStdin = makeLineBufferedStdin(tty);
        await runTool(api, runArgs[0], runArgs, {
          cwd: resolvedConfig.compile.cwd ?? `/home/user/${resolvedConfig.id}`,
          onStdout: (t: string) => {
            tty.write(t.replace(/\n/g, '\r\n'));
          },
          onStderr: (t: string) => {
            tty.write(`\x1b[31m${t.replace(/\n/g, '\r\n')}\x1b[0m`);
          },
          stdin: lineBufferedStdin,
        });
        setExecutionPhase('idle');
        setStatus(`Done (${((performance.now() - tTotal) / 1000).toFixed(1)}s)`);
        return;
      }

      // ── Canvas path ──────────────────────────────────────────────
      if (runType === 'canvas') {
        if (!compileTarget) {
          setExecutionPhase('idle');
          setStatus('No compilable source file found');
          tty.writeError('No .c/.cpp source file found in workspace.');
          return;
        }

        const toolchain = resolveCanvasToolchain(resolvedConfig.compile.toolchain);
        if (!toolchain) {
          setExecutionPhase('idle');
          setStatus('Unsupported canvas toolchain');
          tty.writeError(`Canvas workspaces require an SDL3, raylib, or Allegro preset; received '${resolvedConfig.compile.toolchain}'.`);
          return;
        }

        const compileCwd = resolvedConfig.compile.cwd ?? `/home/user/${resolvedConfig.id}`;
        const wasmPath = resolveWsPath(compileCwd, resolvedConfig.compile.output || 'main.wasm');
        const label = toolchain.startsWith('sdl') ? 'SDL3' : toolchain.startsWith('raylib') ? 'raylib' : 'allegro';
        setStatus('Compiling...');
        tty.writeLine(`\x1b[36m${label} canvas build...\x1b[0m`);

        setCanvasIsRunning(true);
        ensureCanvasTab('right');
        setActiveTabId(`tab:${compileTarget}`);
        await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

        const canvas = canvasRef.current;
        if (!canvas) {
          setCanvasIsRunning(false);
          setExecutionPhase('idle');
          tty.writeError('Canvas element not found.');
          return;
        }
        canvas.dataset.sdlRunning = 'true';
        canvas.style.display = 'block';
        canvas.width = 800;
        canvas.height = 600;

        const result = await api.canvas.buildAndStart(
          {
            toolchain,
            sourcePath: compileTarget,
            cwd: compileCwd,
            wasmPath,
            onStdout: (text) => tty.writeLine(text),
            onStderr: (text) => tty.writeError(text),
          },
          {
            canvas,
            onStdout: (text) => tty.writeLine(text),
            onStderr: (text) => tty.writeError(text),
          },
        );

        if ('phase' in result) {
          delete canvas.dataset.sdlRunning;
          canvas.style.display = 'none';
          setCanvasIsRunning(false);
          setExecutionPhase('idle');
          const failed = result.phase === 'compile' ? result.compile : result.link;
          setStatus(`${label} ${result.phase} failed`);
          tty.writeError(`${label} ${result.phase} failed (exit ${failed.exitCode})`);
          return;
        }

        setExecutionPhase('running');
        setStatus(`${label} done (${((performance.now() - tTotal) / 1000).toFixed(1)}s) — running`);
        tty.writeLine(`\x1b[36m${label} detected (${result.runtimeProfile})\x1b[0m`);
        tty.writeLine(`\x1b[32m${label} rendering in canvas tab →\x1b[0m`);
        return;
      }

      // ── Standard WASI terminal path ─────────────────────────────
      if (!compileTarget) {
        setExecutionPhase('idle');
        setStatus('No compilable source file found');
        tty.writeError('No source file found in workspace.');
        return;
      }
      setStatus('Compiling...');
      tty.writeLine(`Compiling ${compileTarget}...`);

      // ── Direct clang+wasm-ld fast path ──────────────────────────
      // When compile.args is empty, bypass the emcc Python pipeline entirely
      // and use direct clang → wasm-ld (same approach as SDL3 path).  This
      // eliminates ~13s of overhead: Python boot, 8970-file pre-warm,
      // wasm-opt asyncify pass, and wasm-emscripten-finalize.
      // The WASI runtime handles blocking stdin via SharedArrayBuffer +
      // Atomics.wait, so -sASYNCIFY is not needed.
      const useDirectPath = resolvedConfig.compile.args.length === 0 && resolvedConfig.run.type === 'wasi-terminal';

      if (useDirectPath) {
        const sourceFsPath = compileTarget;
        const objPath = '/tmp/emception-terminal-main.o';
        const wasmPath = resolveWsPath(cwd, resolvedConfig.compile.output || 'main.wasm');

        tty.writeLine('\x1b[36mDirect compile (clang -cc1)...\x1b[0m');
        // Use clang -cc1 directly (not driver mode). The driver tries to
        // posix_spawn cc1 as a subprocess which fails silently in the browser
        // (no fork/posix_spawn in emscripten libc), causing clang to exit 0
        // in ~35ms with no output. cc1_main is linked into clang.wasm so the
        // frontend runs in-process when invoked with -cc1 directly.
        // Detect language from source file extension to pick the right preset.
        // C source files (.c) use the C preset; everything else uses C++.
        const isC = compileTarget.endsWith('.c');
        const directPreset = isC ? (EMCEPTION_PRESETS.c as NativePreset) : (EMCEPTION_PRESETS.cpp as NativePreset);
        const presetPaths = { sourcePath: sourceFsPath, objectPath: objPath, wasmPath };
        const clangResult = await runTool(api, directPreset.compileTool, directPreset.compileArgv(presetPaths), {
          cwd: resolvedConfig.compile.cwd ?? `/home/user/${resolvedConfig.id}`,
          onStdout: (t: string) => {
            console.log(t);
            tty.writeLine(t);
          },
          onStderr: (t: string) => {
            console.error(t);
            tty.writeError(t);
          },
        });

        if (clangResult.exitCode !== 0) {
          const dur = ((performance.now() - t0) / 1000).toFixed(2);
          setExecutionPhase('idle');
          setStatus(`Compilation failed (${dur}s)`);
          tty.writeLine(`\x1b[31mCompilation failed (exit ${clangResult.exitCode})\x1b[0m`);
          return;
        }

        tty.writeLine('\x1b[36mLinking (wasm-ld)...\x1b[0m');
        const lldResult = await runTool(api, directPreset.linkTool, directPreset.linkArgv(presetPaths), {
          cwd: resolvedConfig.compile.cwd ?? `/home/user/${resolvedConfig.id}`,
          onStdout: (t: string) => {
            console.log(t);
            tty.writeLine(t);
          },
          onStderr: (t: string) => {
            console.error(t);
            tty.writeError(t);
          },
        });

        if (lldResult.exitCode !== 0) {
          const dur = ((performance.now() - t0) / 1000).toFixed(2);
          setExecutionPhase('idle');
          setStatus(`Link failed (${dur}s)`);
          tty.writeLine(`\x1b[31mLinker step failed (exit ${lldResult.exitCode})\x1b[0m`);
          return;
        }

        const dur = ((performance.now() - t0) / 1000).toFixed(2);
        setStatus('Compilation successful');
        tty.writeLine(`\x1b[32mCompilation successful in ${dur}s\x1b[0m`);

        // Log output size for test verification
        const wasmFile = await api.workspace.readFile(wasmPath);
        const wasmSize = wasmFile ? wasmFile.length : 0;
        console.log(`${P} Compilation output: main.wasm=${wasmSize}B`);

        // Go directly to run phase
        tty.writeLine('Running...');
        const lineBufferedStdin = makeLineBufferedStdin(tty);
        const runArgs = resolvedConfig.run.args ?? ['wasi-run', wasmPath];
        setExecutionPhase('running');
        await runTool(api, runArgs[0], runArgs, {
          cwd: resolvedConfig.compile.cwd ?? `/home/user/${resolvedConfig.id}`,
          onStdout: (t: string) => {
            tty.write(t.replace(/\n/g, '\r\n'));
          },
          onStderr: (t: string) => {
            tty.write(`\x1b[31m${t.replace(/\n/g, '\r\n')}\x1b[0m`);
          },
          stdin: lineBufferedStdin,
        });
        setExecutionPhase('idle');
        setStatus(`Done (${((performance.now() - tTotal) / 1000).toFixed(1)}s)`);
        return;
      }

      // ── emcc path (Python-based pipeline) ───────────────────────
      const compileArgs =
        resolvedConfig.compile.args.length > 0
          ? resolveArgs(resolvedConfig.compile.args, compileTarget)
          : ['emcc', compileTarget, '-o', resolveWsPath(cwd, resolvedConfig.compile.output || 'main.wasm'), '-O2'];
      const result = await runTool(api, compileArgs[0], compileArgs, {
        cwd: resolvedConfig.compile.cwd ?? `/home/user/${resolvedConfig.id}`,
        onStdout: (t: string) => {
          console.log(t);
          tty.writeLine(t);
        },
        onStderr: (t: string) => {
          console.error(t);
          tty.writeError(t);
        },
      });
      const duration = ((performance.now() - t0) / 1000).toFixed(2);
      if (result.exitCode !== 0) {
        setExecutionPhase('idle');
        setStatus('Compilation failed');
        tty.writeLine(`\x1b[31mCompilation failed (exit ${result.exitCode})\x1b[0m`);
        return;
      }
      setStatus('Compilation successful');
      tty.writeLine(`\x1b[32mCompilation successful in ${duration}s\x1b[0m`);
      const wasmPath = resolveWsPath(cwd, resolvedConfig.compile.output || 'main.wasm');
      let wasmBytes = await api.workspace.readFile(wasmPath);

      // emcc may return before all subprocess-linked outputs are flushed to VFS.
      // Give the canonical artifact a short grace window before triggering fallback.
      if ((!wasmBytes || wasmBytes.length === 0) && resolvedConfig.run.type === 'wasi-terminal' && resolvedConfig.compile.tool === 'emcc') {
        const waitUntil = performance.now() + 12_000;
        while ((!wasmBytes || wasmBytes.length === 0) && performance.now() < waitUntil) {
          await new Promise<void>((resolve) => setTimeout(resolve, 120));
          wasmBytes = await api.workspace.readFile(wasmPath);
        }
      }

      if ((!wasmBytes || wasmBytes.length === 0) && resolvedConfig.run.type === 'wasi-terminal' && resolvedConfig.compile.tool === 'emcc') {
        tty.writeLine('\x1b[33mOutput artifact missing after emcc; attempting fallback link pipeline...\x1b[0m');
        const fallbackObj = '/tmp/emception-fallback-main.o';
        const sourceFsPath = compileTarget;

        const clangFallback = await runTool(api,
          'clang',
          [
            'clang',
            '--target=wasm32-unknown-emscripten',
            '--sysroot=/usr/lib/emscripten/cache/sysroot',
            '-Xclang',
            '-iwithsysroot/include/fakesdl',
            '-Xclang',
            '-iwithsysroot/include/compat',
            '-O2',
            '-c',
            sourceFsPath,
            '-o',
            fallbackObj,
          ],
          {
            cwd: resolvedConfig.compile.cwd ?? `/home/user/${resolvedConfig.id}`,
            onStdout: (t: string) => tty.writeLine(t),
            onStderr: (t: string) => tty.writeError(t),
          },
        );

        if (clangFallback.exitCode === 0) {
          const crtPath = '/usr/lib/emscripten/cache-lib/wasm32-emscripten/crt1.o';
          const crtBytes = await api.workspace.readFile(crtPath);
          const magic = crtBytes
            ? Array.from((crtBytes as Uint8Array).slice(0, 8))
              .map((b: number) => b.toString(16).padStart(2, '0'))
              .join('')
            : 'none';
          tty.writeLine(`crt1.o probe: bytes=${crtBytes?.length ?? 0}, head=${magic}`);

          const lldFallback = await runTool(api,
            'wasm-ld',
            [
              'wasm-ld',
              fallbackObj,
              '-o',
              wasmPath,
              '-L/usr/lib/emscripten/cache/sysroot/lib/wasm32-emscripten',
              '-L/usr/lib/emscripten/src/lib',
              '/usr/lib/emscripten/cache/sysroot/lib/wasm32-emscripten/crt1.o',
              '--entry=main',
              '--import-undefined',
              '--allow-undefined',
              '--export-table',
              '--table-base=1',
              '--global-base=1024',
              '-z',
              'stack-size=1048576',
              '-lGL-getprocaddr',
              '-lal',
              '-lhtml5',
              '-lstubs',
              '-lc',
              '-ldlmalloc',
              '-lcompiler_rt',
              '-lc++-noexcept',
              '-lc++abi-noexcept',
              '-lsockets',
            ],
            {
              cwd: resolvedConfig.compile.cwd ?? `/home/user/${resolvedConfig.id}`,
              onStdout: (t: string) => tty.writeLine(t),
              onStderr: (t: string) => tty.writeError(t),
            },
          );

          if (lldFallback.exitCode !== 0) {
            tty.writeLine('\x1b[31mFallback linker step failed.\x1b[0m');
          }
        } else {
          tty.writeLine('\x1b[31mFallback compile step failed.\x1b[0m');
        }

        wasmBytes = await api.workspace.readFile(wasmPath);
      }
      console.log(`${P} Compilation output: main.wasm=${wasmBytes?.length ?? 0}`);
      tty.writeLine('Running...');
      const lineBufferedStdin = makeLineBufferedStdin(tty);
      const runArgs = resolvedConfig.run.args ?? ['wasi-run', resolvedConfig.compile.output || '/home/user/main.wasm'];
      setExecutionPhase('running');
      await runTool(api, runArgs[0], runArgs, {
        cwd: resolvedConfig.compile.cwd ?? `/home/user/${resolvedConfig.id}`,
        onStdout: (t: string) => {
          tty.write(t.replace(/\n/g, '\r\n'));
        },
        onStderr: (t: string) => {
          tty.write(`\x1b[31m${t.replace(/\n/g, '\r\n')}\x1b[0m`);
        },
        stdin: lineBufferedStdin,
      });
      setExecutionPhase('idle');
      setStatus(`Done (${((performance.now() - tTotal) / 1000).toFixed(1)}s)`);
    } catch (e) {
      setExecutionPhase('idle');
      if (!stoppedRef.current) {
        console.error(`${P} Exception:`, e);
        setStatus('Error during execution');
        tty.writeError(String(e));
      }
    } finally {
      console.log(`${P} COMPILE & RUN COMPLETE`);
      restoreEditorFocus();
    }
  };

  const handleTest = async () => {
    const testConfig = resolvedConfig.test;
    const api = apiRef.current;
    const tty = terminalIORef.current;
    if (!api || !tty || !testConfig) return;
    stoppedRef.current = false;
    setExecutionPhase('compiling');
    setActiveTerminalId('terminal-1');
    const restoreEditorFocus = () => {
      editorRef.current?.focus();
    };
    const tTotal = performance.now();
    try {
      tty.clear();
      tty.writeLine('\x1b[36mRunning tests...\x1b[0m');

      // Sync files to VFS
      const textFiles = Object.values(files).filter((f) => f.type === 'text' && isTextFile(f.path));
      const enc = new TextEncoder();
      for (const file of textFiles) {
        await api.workspace.writeFile(file.path, enc.encode(file.content));
      }

      // Compile test if needed
      if (testConfig.compileArgs && testConfig.compileArgs.length > 0) {
        setStatus('Compiling tests...');
        const compileResult = await runTool(api, testConfig.tool, testConfig.compileArgs, {
          cwd: resolvedConfig.compile.cwd ?? `/home/user/${resolvedConfig.id}`,
          onStdout: (t: string) => {
            tty.writeLine(t);
          },
          onStderr: (t: string) => {
            tty.writeError(t);
          },
        });
        if (compileResult.exitCode !== 0) {
          setExecutionPhase('idle');
          setStatus('Test compilation failed');
          tty.writeLine(`\x1b[31mTest compilation failed (exit ${compileResult.exitCode})\x1b[0m`);
          return;
        }
      }

      // Run tests
      setStatus('Running tests...');
      setExecutionPhase('running');
      const lineBufferedStdin = makeLineBufferedStdin(tty);
      const runResult = await runTool(api, testConfig.runArgs[0], testConfig.runArgs, {
        cwd: resolvedConfig.compile.cwd ?? `/home/user/${resolvedConfig.id}`,
        onStdout: (t: string) => {
          tty.write(t.replace(/\n/g, '\r\n'));
        },
        onStderr: (t: string) => {
          tty.write(`\x1b[31m${t.replace(/\n/g, '\r\n')}\x1b[0m`);
        },
        stdin: lineBufferedStdin,
      });

      setExecutionPhase('idle');
      const duration = ((performance.now() - tTotal) / 1000).toFixed(1);
      if (runResult.exitCode === 0) {
        setStatus(`Tests passed (${duration}s)`);
        tty.writeLine(`\x1b[32m✓ All tests passed (${duration}s)\x1b[0m`);
      } else {
        setStatus(`Tests failed (${duration}s)`);
        tty.writeLine(`\x1b[31m✗ Tests failed (exit ${runResult.exitCode}, ${duration}s)\x1b[0m`);
      }
    } catch (e) {
      setExecutionPhase('idle');
      if (!stoppedRef.current) {
        setStatus('Test error');
        tty.writeError(String(e));
      }
    } finally {
      restoreEditorFocus();
    }
  };

  const handleStop = async () => {
    if (executionPhase !== 'running') return;

    // SDL3 path: the RAF loop runs on the main thread; pause it and clean up.
    // The worker is idle (it only compiled), so we can keep it for re-compile.
    if (teardownSdlRuntime()) {
      setExecutionPhase('idle');
      setStatus('Stopped');
      xtermRef.current?.writeln('\x1b[33mExecution stopped.\x1b[0m');
      editorRef.current?.focus();
      return;
    }

    // WASI path: the worker is actively running wasi-run — terminate and reboot.
    const api = apiRef.current;
    if (!api || !ownsApiRef.current) return;
    stoppedRef.current = true;
    api.dispose();
    apiRef.current = null;
    ownsApiRef.current = false;
    setExecutionPhase('idle');
    setIsReady(false);
    setStatus('Stopped — rebooting...');
    xtermRef.current?.writeln('\x1b[33mExecution stopped.\x1b[0m');
    await doBootstrap();
    editorRef.current?.focus();
  };

  /** Shared resize-handle style for all Separator instances */
  const resizerStyle: React.CSSProperties = {
    width: 4,
    background: '#313244',
    cursor: 'col-resize',
    transition: 'background 0.15s',
  };
  const resizerVStyle: React.CSSProperties = { ...resizerStyle, width: '100%', height: 4, cursor: 'row-resize' };
  const canRecompileWhileRunning = executionPhase === 'running' && resolvedConfig.run.type === 'canvas';
  const canCompile = isReady && activeFile?.type === 'text' && (executionPhase === 'idle' || canRecompileWhileRunning);
  const showCompileButton = executionPhase !== 'running' || canRecompileWhileRunning;
  const renderExtensionSlot = (slot: keyof Pick<IdeExtension, 'toolbarEnd' | 'explorerFooter' | 'bottomPanel'>) => {
    if (!publishedController) return null;
    return activeExtensions.map((extension) => {
      const content = extension[slot]?.(publishedController);
      return content === undefined || content === null ? null : <Fragment key={extension.id}>{content}</Fragment>;
    });
  };

  const ideContent = (
    <div
      className="emception-ide"
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        width: '100%',
        fontFamily: 'system-ui, sans-serif',
        ...(fullscreen ? { position: 'fixed', inset: 0, zIndex: 9999, background: '#181825' } : {}),
      }}
    >
      {/* Hidden log for Playwright E2E assertions — not visible to users */}
      <pre data-testid="terminal" ref={terminalLogRef} hidden aria-hidden="true" style={{ display: 'none' }} />
      {/* Hidden holder keeps the SDL canvas alive across dock moves. Gated by enableCanvas. */}
      {enableCanvas && (
        <div ref={canvasHolderRef} style={{ position: 'absolute', width: 0, height: 0, overflow: 'hidden', pointerEvents: 'none' }}>
          <canvas id="canvas" data-testid="sdl-canvas" tabIndex={0} ref={canvasRef} style={{ width: '100%', height: '100%', display: 'none' }} />
        </div>
      )}
      {/* ── Title bar ── */}
      <header
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0 1rem',
          height: 36,
          background: '#181825',
          borderBottom: '1px solid #313244',
          flexShrink: 0,
        }}
      >
        <h1 style={{ fontSize: '0.8rem', fontWeight: 600, color: '#cdd6f4', margin: 0, letterSpacing: '0.03em' }}>{title}</h1>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          {/* Workspace preset picker */}
          {!workspaceConfig && !workspaceUrl && (
            <select
              data-testid="workspace-picker"
              value={activePresetId}
              onChange={(e) => switchWorkspace(e.target.value)}
              style={{
                height: 24,
                fontSize: '0.72rem',
                borderRadius: 4,
                border: '1px solid #45475a',
                background: '#1e1e2e',
                color: '#cdd6f4',
                cursor: 'pointer',
                padding: '0 0.4rem',
              }}
            >
              {PRESET_IDS.map((id) => (
                <option key={id} value={id}>
                  {PRESETS[id].label}
                </option>
              ))}
            </select>
          )}
          <span data-testid="status" style={{ fontSize: '0.72rem', color: '#a6adc8' }}>
            {status}
          </span>
          {showCompileButton && (
            <button
              data-testid="compile-button"
              onClick={handleCompile}
              disabled={!canCompile}
              style={{
                height: 24,
                padding: '0 0.75rem',
                fontSize: '0.8rem',
                fontWeight: 500,
                borderRadius: 4,
                border: 'none',
                cursor: canCompile ? 'pointer' : 'not-allowed',
                background: canCompile ? '#a6e3a1' : '#313244',
                color: canCompile ? '#11111b' : '#585b70',
              }}
            >
              {resolvedConfig.run.type === 'python-script' ? '▶ Run' : canRecompileWhileRunning ? '↻' : '▶'}
            </button>
          )}
          {executionPhase === 'running' && (
            <button
              data-testid="stop-button"
              onClick={handleStop}
              style={{
                height: 24,
                padding: '0 0.75rem',
                fontSize: '0.8rem',
                fontWeight: 500,
                borderRadius: 4,
                border: 'none',
                cursor: 'pointer',
                background: '#f38ba8',
                color: '#11111b',
              }}
            >
              &#9632; Stop
            </button>
          )}
          {resolvedConfig.features.showTestButton && resolvedConfig.test && (
            <button
              data-testid="test-button"
              onClick={handleTest}
              disabled={executionPhase !== 'idle' || !isReady}
              style={{
                height: 24,
                padding: '0 0.75rem',
                fontSize: '0.8rem',
                fontWeight: 500,
                borderRadius: 4,
                border: 'none',
                cursor: executionPhase === 'idle' && isReady ? 'pointer' : 'not-allowed',
                background: executionPhase === 'idle' && isReady ? '#cba6f7' : '#313244',
                color: executionPhase === 'idle' && isReady ? '#11111b' : '#585b70',
              }}
            >
              ✓ Test
            </button>
          )}
          <button
            onClick={resetWorkspace}
            style={{
              height: 24,
              padding: '0 0.6rem',
              fontSize: '0.8rem',
              borderRadius: 4,
              border: '1px solid #45475a',
              cursor: 'pointer',
              background: 'transparent',
              color: '#a6adc8',
            }}
          >
            Reset
          </button>
          {renderExtensionSlot('toolbarEnd')}
        </div>
      </header>

      {/* ── Main body: sidebar | editor + terminal ── */}
      <Group orientation="horizontal" style={{ flex: 1, overflow: 'hidden' }}>
        {enableFileExplorer && (
          <>
            {/* Sidebar */}
            <Panel defaultSize="18" minSize="10" maxSize="40" style={{ overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
              <FileExplorer
                files={files}
                selectedPath={selectedPath}
                expandedDirs={expandedDirs}
                fileTree={fileTree}
                onSelectPath={setSelectedPath}
                onToggleDir={(path) =>
                  setExpandedDirs((prev) => {
                    const next = new Set(prev);
                    if (next.has(path)) next.delete(path);
                    else next.add(path);
                    return next;
                  })
                }
                onOpenTab={(path) => ensureOpenTab(path, 'main')}
                onCreateFile={createFile}
                onRename={renameSelectedFile}
                onDelete={deleteSelectedFile}
                allowFileCreation={allowFileCreation}
                footer={renderExtensionSlot('explorerFooter')}
              />
            </Panel>
            <Separator style={resizerStyle} />
          </>
        )}

        {/* Editor + terminal column */}
        <Panel style={{ display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          <Group orientation="vertical" style={{ flex: 1, overflow: 'hidden' }}>
            {/* Editor row: main (+ optional right panel) */}
            <Panel style={{ display: 'flex', overflow: 'hidden' }}>
              <Group orientation="horizontal" style={{ flex: 1, overflow: 'hidden' }}>
                <Panel minSize="20" style={{ overflow: 'hidden' }}>
                  <DockGroupPanel
                    key={activePresetId}
                    group="main"
                    tabs={groupTabs('main')}
                    activeTabId={activeTabId}
                    files={files}
                    onCanvasHost={handleCanvasHost}
                    onSetActiveTab={setActiveTabId}
                    onCloseTab={closeTab}
                    onMoveTab={moveTabToGroup}
                    onReorderTab={reorderTab}
                    onEditorMount={handleEditorDidMount}
                    onEditorChange={handleEditorChange}
                    canvasIsRunning={canvasIsRunning}
                    readOnly={readOnly}
                  />
                </Panel>
                {hasRightGroup && (
                  <>
                    <Separator style={resizerStyle} />
                    <Panel defaultSize="35" minSize="15" style={{ overflow: 'hidden' }}>
                      <DockGroupPanel
                        key={`${activePresetId}-right`}
                        group="right"
                        tabs={groupTabs('right')}
                        activeTabId={activeTabId}
                        files={files}
                        onCanvasHost={handleCanvasHost}
                        onSetActiveTab={setActiveTabId}
                        onCloseTab={closeTab}
                        onMoveTab={moveTabToGroup}
                        onReorderTab={reorderTab}
                        onEditorMount={handleEditorDidMount}
                        onEditorChange={handleEditorChange}
                        canvasIsRunning={canvasIsRunning}
                        readOnly={readOnly}
                      />
                    </Panel>
                  </>
                )}
              </Group>
            </Panel>

            {hasBottomGroup && (
              <>
                <Separator style={resizerVStyle} />
                <Panel defaultSize="25" minSize="10" style={{ overflow: 'hidden' }}>
                  <DockGroupPanel
                    key={`${activePresetId}-bottom`}
                    group="bottom"
                    tabs={groupTabs('bottom')}
                    activeTabId={activeTabId}
                    files={files}
                    onCanvasHost={handleCanvasHost}
                    onSetActiveTab={setActiveTabId}
                    onCloseTab={closeTab}
                    onMoveTab={moveTabToGroup}
                    onReorderTab={reorderTab}
                    onEditorMount={handleEditorDidMount}
                    onEditorChange={handleEditorChange}
                    canvasIsRunning={canvasIsRunning}
                    readOnly={readOnly}
                  />
                </Panel>
              </>
            )}

            {enableTerminal && (
              <>
                {/* Terminal */}
                <Separator style={resizerVStyle} />
                <Panel defaultSize="28" minSize="8" style={{ overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
                  <TerminalPanel
                    terminalTabs={terminalTabs}
                    activeTerminalId={activeTerminalId}
                    onSetActiveTerminal={setActiveTerminalId}
                    onNewTerminal={createTerminalTab}
                    onCloseTerminal={closeTerminalTab}
                    onBootTerminalReady={handleBootTerminalReady}
                  />
                </Panel>
              </>
            )}
          </Group>
        </Panel>
      </Group>

      {renderExtensionSlot('bottomPanel')}

      {/* ── Status bar ── */}
      <div
        style={{
          height: 22,
          background: '#89b4fa',
          display: 'flex',
          alignItems: 'center',
          padding: '0 0.75rem',
          gap: '1rem',
          flexShrink: 0,
          fontSize: '0.7rem',
          color: '#11111b',
        }}
      >
        <span>{isReady ? '✓ Ready' : '⟳ Booting...'}</span>
        {activeFile && <span>{activeFile.path}</span>}
        {activeFile?.type === 'text' && <span style={{ marginLeft: 'auto' }}>{inferLanguage(activeFileName).toUpperCase()}</span>}
      </div>
    </div>
  );
  return fullscreen && typeof document !== 'undefined' ? createPortal(ideContent, document.body) : ideContent;
}
