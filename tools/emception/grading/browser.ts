import { createEmception } from '@gameguild/emception-browser';
import { runTests, withWorkspaceOverlay } from 'emception/testing';
import { resolveBuild, ToolchainPreset, type TestPlan } from 'emception';
import { buildAssessmentExecutionPlan } from '../../../packages/infrastructure/ui-emception/src/assessment/plan';
import { createAssessmentWorkspaceConfig, type CodingLanguage } from '../../../packages/infrastructure/ui-emception/src/assessment/presets';
import type { CodingAssessmentDefinition } from '../../../packages/infrastructure/ui-emception/src/assessment/types';

interface CodeWorkerRequest {
  definition: CodingAssessmentDefinition;
  files: Record<string, { content: string; encoding: 'text' }>;
}

/** Runs only in a fresh, server-owned Chromium context. This is never a learner API. */
export async function executeCodeAssessment(request: CodeWorkerRequest): Promise<boolean[]> {
  const definition = request.definition;
  const canonical = (path: string) => '/home/user/' + path.replace(/^\/(?:home\/user|user)\//, '');
  const files = Object.fromEntries(Object.entries(definition.Data.Files).map(([path, file]) => [
    canonical(path), { encoding: file.Encoding ?? 'text', content: file.Content },
  ]));
  for (const [path, file] of Object.entries(request.files)) {
    files[canonical(path)] = file;
  }
  const workspace = createAssessmentWorkspaceConfig(definition.Environment.Language as CodingLanguage, files);
  const sources = Object.keys(files).filter((path) => /\.(?:c|cc|cpp|cxx)$/i.test(path));
  const build = resolveBuild({ preset: workspace.compile.toolchain,
    callsite: { sources, output: workspace.compile.output } });
  if (build.toolchain === ToolchainPreset.CMake || build.toolchain === ToolchainPreset.Python) {
    throw new Error('This Code adapter requires a native compilation preset.');
  }
  const api = await createEmception({ tty: 'none', manifestUrl: '/cdn/manifest.json' });
  try {
    let infrastructureFailure: unknown;
    const compile = api.compileAndRun.bind(api);
    api.compileAndRun = async (...args) => {
      try {
        const result = await compile(...args);
        if (result.timedOut || result.stdout.length + result.stderr.length > 2_000_000) {
          throw new Error('Code execution exceeded its runtime or output budget.');
        }
        return result;
      } catch (error) { infrastructureFailure = error; throw error; }
    };
    const run = api.run.bind(api);
    api.run = async (...args) => {
      try { return await run(...args); }
      catch (error) { infrastructureFailure = error; throw error; }
    };
    await api.workspace.setBuild(build);
    for (const [path, file] of Object.entries(files)) {
      const data = file.encoding === 'base64'
        ? Uint8Array.from(atob(file.content), (character) => character.charCodeAt(0))
        : file.content;
      await api.workspace.writeFile(path, data);
    }
    const execution = buildAssessmentExecutionPlan(definition, 'full');
    // Build paths must refer to exactly the persisted workspace files. Header files
    // are available to includes, but are not compiled as translation units.
    const plan: TestPlan = {
      ...execution.plan,
      build,
      timeoutMsPerCase: 30_000,
    };
    const overlay = execution.overlay.map((file) => ({ ...file, path: canonical(file.path) }));
    const report = await withWorkspaceOverlay(api.workspace, overlay,
      () => runTests(api, plan));
    // The reusable preview engine represents errors as failed cases. Official
    // grading must distinguish a failed student test from unavailable execution.
    if (infrastructureFailure) throw infrastructureFailure;
    if (report.cases.length !== plan.cases.length) throw new Error('Incomplete Code test report.');
    return report.cases.map((test) => test.passed);
  } finally {
    api.dispose();
  }
}
