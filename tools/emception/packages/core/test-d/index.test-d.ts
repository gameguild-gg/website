/**
 * emception - tsd type tests
 *
 * Run: pnpm --filter emception run test:types
 * Requires a build first (handled by the script).
 */
import type {
    EmceptionAPI,
    FileEntry,
    FSStats,
    IFileSystem,
    NativeBuildConfig,
    RunOptions,
    TestCase,
    TestCaseResult,
    TestPlan,
    TestReport,
    ToolResult,
    WorkspaceAPI,
    WorkspaceBuildConfig,
    WorkspaceOptions,
    WorkspaceSeed,
    WorkspaceSeedPolicy,
} from 'emception';
import { ToolchainPreset } from 'emception';
import {
    BuildConfigError,
    CanvasUnavailableError,
    CrossOriginIsolationError,
    EmceptionError,
    RuntimeFeatureUnavailableError,
    TestFailureError,
    TimeoutError,
    WorkspaceConflictError,
} from 'emception/errors';
import { expectAssignable, expectError, expectType } from 'tsd';

// ── Error hierarchy ──────────────────────────────────────────────────────────

expectAssignable<Error>(new EmceptionError('msg'));
expectAssignable<EmceptionError>(new TimeoutError('timeout'));
expectAssignable<EmceptionError>(new WorkspaceConflictError('conflict'));
expectAssignable<EmceptionError>(new TestFailureError('fail'));
expectAssignable<EmceptionError>(new BuildConfigError('bad build'));
expectAssignable<EmceptionError>(new RuntimeFeatureUnavailableError('no feature'));
expectAssignable<RuntimeFeatureUnavailableError>(new CrossOriginIsolationError('no coi'));
expectAssignable<RuntimeFeatureUnavailableError>(new CanvasUnavailableError('no canvas'));

// ── FileEntry ────────────────────────────────────────────────────────────────

const fe: FileEntry = { content: 'hello' };
expectType<string | Uint8Array>(fe.content);
expectType<'public' | 'hidden' | 'solution' | undefined>(fe.visibility);
expectType<boolean | undefined>(fe.readonly);
expectType<boolean | undefined>(fe.executable);

// shorthand string is assignable as a WorkspaceSeed value
const seed: WorkspaceSeed = {
    'main.cpp': 'int main() {}',
    'helper.h': { content: '#pragma once', visibility: 'hidden' },
};
expectAssignable<WorkspaceSeed>(seed);

// ── WorkspaceBuildConfig ──────────────────────────────────────────────────────

const build: WorkspaceBuildConfig = {
    toolchain: ToolchainPreset.CPP,
    compiler: 'em++',
    flags: ['-std=c++20', '-O2'],
    ldflags: ['-sEXIT_RUNTIME=1'],
};
expectAssignable<WorkspaceBuildConfig>(build);

// compiler must be one of the allowed literals
expectError<NativeBuildConfig['compiler']>('gcc');

// ── WorkspaceOptions ─────────────────────────────────────────────────────────

const opts: WorkspaceOptions = { name: 'lesson-01' };
expectAssignable<WorkspaceOptions>(opts);
expectType<WorkspaceSeedPolicy | undefined>(opts.seedPolicy);

// ── RunOptions ───────────────────────────────────────────────────────────────

const runOpts: RunOptions = {};
expectType<string | undefined>(runOpts.workspace);
expectType<number | undefined>(runOpts.timeoutMs);
expectType<AbortSignal | undefined>(runOpts.signal);

// ── ToolResult ───────────────────────────────────────────────────────────────

declare const result: ToolResult;
expectType<number>(result.exitCode);
expectType<string>(result.stdout);
expectType<string>(result.stderr);
expectType<number>(result.durationMs);
expectType<boolean>(result.timedOut);
expectType<string | undefined>(result.signal);

// ── TestCase discriminated union ──────────────────────────────────────────────

const stdioCase: TestCase = {
    kind: 'stdio',
    expectedStdout: 'Hello',
};
expectAssignable<TestCase>(stdioCase);

const customCase: TestCase = {
    kind: 'custom',
    run: async (_em: EmceptionAPI): Promise<TestCaseResult> => ({
        name: 'my test',
        passed: true,
        durationMs: 0,
    }),
};
expectAssignable<TestCase>(customCase);

// ── TestPlan ──────────────────────────────────────────────────────────────────

const plan: TestPlan = {
    cases: [stdioCase],
    redactHidden: true,
};
expectAssignable<TestPlan>(plan);

// ── TestReport ────────────────────────────────────────────────────────────────

declare const report: TestReport;
expectType<number>(report.passed);
expectType<number>(report.failed);
expectType<number>(report.totalDurationMs);
expectType<TestCaseResult[]>(report.cases);

// ── WorkspaceAPI ──────────────────────────────────────────────────────────────

declare const wsApi: WorkspaceAPI;
expectType<() => Promise<string[]>>(wsApi.list);
expectType<(name: string) => Promise<void>>(wsApi.switch);
expectType<() => Promise<Blob>>(wsApi.exportZip);

// ── EmceptionAPI ──────────────────────────────────────────────────────────────

declare const em: EmceptionAPI;
expectAssignable<WorkspaceAPI>(em.workspace);
expectType<() => void>(em.dispose);

// Optional filesystem capabilities remain available through the shared contract.
declare const filesystem: IFileSystem;
expectType<(() => void | Promise<void>) | undefined>(filesystem.clear);
expectType<((path: string) => Uint8Array | null) | undefined>(filesystem.readFileSync);
expectType<((path: string, data: Uint8Array) => void) | undefined>(filesystem.writeFileSync);
expectType<((path: string) => boolean) | undefined>(filesystem.existsSync);
expectType<((path: string) => FSStats | null) | undefined>(filesystem.statSync);
expectType<((path: string) => string[]) | undefined>(filesystem.readdirSync);
expectType<((path: string) => boolean) | undefined>(filesystem.deleteFileSync);
expectType<((path: string) => void) | undefined>(filesystem.mkdirSync);

if (typeof filesystem.readFileSync === 'function') {
    expectType<Uint8Array | null>(filesystem.readFileSync('/entry'));
}
if (typeof filesystem.clear === 'function') {
    expectType<void | Promise<void>>(filesystem.clear());
}
