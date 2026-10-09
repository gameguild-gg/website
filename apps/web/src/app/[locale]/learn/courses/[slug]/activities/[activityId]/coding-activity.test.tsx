import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { useLayoutEffect, type ReactNode } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => {
  const EditorRender = vi.fn();
  return {
    auth: vi.fn(),
    buildPlan: vi.fn(),
    createWorkspace: vi.fn(),
    EditorRender,
    getCodingAssignmentPublic: vi.fn(),
    getCourseAccessData: vi.fn(),
    getCourseLearnerContext: vi.fn(),
    getMyProjects: vi.fn(),
    getMySubmissions: vi.fn(),
    getToken: vi.fn(),
    submitAssessment: vi.fn(),
    startRuntimeSubmission: vi.fn(),
    submitRuntimeSubmission: vi.fn(),
    computeScore: vi.fn(),
    useRouterPush: vi.fn(),
  };
});

vi.mock('@/lib/learner/courses', () => ({ getCourseAccessData: mocks.getCourseAccessData }));
vi.mock('@/lib/learner/records', () => ({
  getCourseLearnerContext: mocks.getCourseLearnerContext,
  getMyProjects: mocks.getMyProjects,
}));
vi.mock('@/auth', () => ({ auth: mocks.auth, getToken: mocks.getToken }));
vi.mock('@game-guild/client', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@game-guild/client')>();
  class LearningAssessmentsModuleStub {
    getAssessmentsMySubmissions = mocks.getMySubmissions;
  }
  return {
    ...actual,
    GeneratedApi: { ...actual.GeneratedApi, LearningAssessmentsModule: LearningAssessmentsModuleStub },
  };
});
vi.mock('@/lib/coding-assignment/client', () => ({ getCodingAssignmentPublic: mocks.getCodingAssignmentPublic }));
vi.mock('@/lib/learner/activity-actions', () => ({ submitAssessment: mocks.submitAssessment }));
vi.mock('@/lib/learning/grading-runtime-actions', () => ({
  startIndividualRuntimeSubmission: mocks.startRuntimeSubmission,
  submitRuntimeSubmission: mocks.submitRuntimeSubmission,
}));
vi.mock('@/lib/emception/scoring', () => ({ computeScore: mocks.computeScore }));
vi.mock('next/navigation', () => ({
  usePathname: () => '/workspace/learning',
  notFound: () => { throw new Error('not-found'); },
}));
vi.mock('@/i18n/navigation', () => ({
  useRouter: () => ({ push: mocks.useRouterPush }),
  Link: ({ children, href }: { children: ReactNode; href: string }) => <a href={href}>{children}</a>,
}));
vi.mock('@game-guild/emception-ui/assessment/editor', () => ({ CodingAssessmentEditor: mocks.EditorRender }));
vi.mock('@game-guild/emception-ui/assessment/plan', () => ({ buildAssessmentExecutionPlan: mocks.buildPlan }));
vi.mock('@game-guild/emception-ui/assessment/presets', () => ({
  createAssessmentWorkspaceConfig: mocks.createWorkspace,
}));
vi.mock('@game-guild/emception-ui/assessment/storage', () => ({
  workspaceStorageKey: (token: string, workspaceId: string) => `gameguild.emception.workspace.${token}.${workspaceId}.v2`,
}));

import LearnerActivityPage from './page';
import { CodingActivityClient } from '@/components/learning/coding-activity-client';

function makeAssessment(overrides: Record<string, unknown> = {}) {
  return {
    id: 'assessment-1',
    courseId: 'course-1',
    contentId: 'content-1',
    title: 'Coding Assessment',
    description: 'Write code',
    type: 'Assignment',
    maxScore: 100,
    passingScore: 60,
    submissionModalities: 'Code',
    dueAt: null,
    ...overrides,
  };
}

function makeAssignment(overrides: Record<string, unknown> = {}) {
  return {
    Type: 'coding-assignment',
    Version: 1,
    Environment: { Language: 'cpp', Tools: '', AllowStudentCreateFiles: true },
    Data: {
      Files: {
        'main.cpp': {
          Content: '// starter', Encoding: 'text', Visibility: 'Public', Modifiable: true,
        },
      },
    },
    Tests: { Public: [], Private: [] },
    Grading: { MaxScore: 100, PassingScore: 60 },
    ...overrides,
  };
}

function makeReadyAccess() {
  return {
    kind: 'ready' as const,
    course: {
      id: 'course-1', title: 'Test Course', slug: 'test-course', description: '', thumbnail: null,
      modules: [], overallProgress: 0, totalItems: 0, completedItems: 0, remainingMinutes: 0,
      enrollmentId: 'enrollment-1',
    },
  };
}

function useOfficialDefinition(definition: ReturnType<typeof makeAssignment>) {
  mocks.startRuntimeSubmission.mockResolvedValue({ success: true, data: {
    submissionId: 'official-submission-1',
    execution: { delivery: { itemOrder: ['code'], items: { code: {
      adapterKey: 'code-assessment-type', adapterVersion: '1', learnerPayload: { definition },
    } } } },
  } });
}

function makeContext(assessment: ReturnType<typeof makeAssessment>) {
  return {
    enrollmentId: 'enrollment-1', cohort: null, calendar: [], assessmentGroups: [],
    assessments: [assessment], submissions: [], discussions: [], certificates: [],
  };
}

function pageParams(activityId = 'assessment-assessment-1', slug = 'test-course') {
  return { params: Promise.resolve({ activityId, slug }) };
}

function stubAssessmentEditor(delta: Array<{ path: string; content: string }> = []) {
  const session = {
    run: vi.fn(async () => undefined),
    getSubmissionDelta: vi.fn(async () => delta),
  };
  mocks.EditorRender.mockImplementation((props: {
    onSessionReady?: (next: typeof session) => void;
    [key: string]: unknown;
  }) => {
    const { onSessionReady, ...rest } = props;
    useLayoutEffect(() => {
      onSessionReady?.(session);
    }, [onSessionReady]);
    return <div data-testid="mock-assessment-editor" data-props={JSON.stringify(rest)}>assessment editor</div>;
  });
  return session;
}

describe('coding activity page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getCourseAccessData.mockResolvedValue(makeReadyAccess());
    mocks.getCourseLearnerContext.mockResolvedValue(makeContext(makeAssessment()));
    mocks.auth.mockResolvedValue({ user: { id: 'user-1' } });
    mocks.getToken.mockResolvedValue('token-1');
    mocks.getMySubmissions.mockResolvedValue({ ok: true, data: [] });
    mocks.getMyProjects.mockResolvedValue([]);
    mocks.getCodingAssignmentPublic.mockResolvedValue(makeAssignment());
    useOfficialDefinition(makeAssignment());
    mocks.computeScore.mockReturnValue({ score: 0, passed: false, feedback: '' });
    mocks.createWorkspace.mockImplementation((language: string, files: unknown) => ({
      id: language,
      label: 'Assessment workspace',
      compile: { tool: 'clang', args: [], output: 'main.wasm', toolchain: 'cpp' },
      run: { type: 'wasi-terminal', tool: 'wasi-run', args: ['wasi-run', 'main.wasm'] },
      features: { canvas: false },
      files,
    }));
    mocks.buildPlan.mockImplementation((assignment: { Tests: { Public?: Array<{ kind: string; Name?: string; Weight?: number }> } }) => ({
      plan: {
        cases: (assignment.Tests.Public ?? []).map((test) => ({
          kind: test.kind === 'functional' ? 'doctest' : 'stdio', name: test.Name, weight: test.Weight,
        })),
      },
      overlay: [],
      weights: [],
    }));
    stubAssessmentEditor();
  });

  it('keeps the browser-only IDE out of the server render', () => {
    const markup = renderToStaticMarkup(
      <CodingActivityClient
        assessmentId="assessment-1"
        enrollmentId="enrollment-1"
        courseId="course-1"
        slug="test-course"
        assignment={makeAssignment()}
      />,
    );

    expect(markup).toContain('data-testid="ide-skeleton"');
    expect(mocks.EditorRender).not.toHaveBeenCalled();
    expect(
      readFileSync(
        resolve(process.cwd(), 'src/components/learning/coding-activity-client.tsx'),
        'utf8',
      ),
    ).not.toContain("from 'next/dynamic'");
  });

  it('mounts the composed assessment editor with public seed, a per-user draft key, and no legacy IDE props', async () => {
    mocks.getCodingAssignmentPublic.mockResolvedValue(makeAssignment());

    render(await LearnerActivityPage(pageParams()));

    expect(mocks.getCodingAssignmentPublic).toHaveBeenCalledWith('course-1', 'content-1');
    const editor = await screen.findByTestId('mock-assessment-editor');
    expect(mocks.startRuntimeSubmission).toHaveBeenCalledWith('assessment-1', 'enrollment-1', expect.any(String));
    const props = JSON.parse(editor.dataset.props ?? '{}');
    expect(props.mode).toBe('learner');
    expect(props.workspaceStorageKey).toBe('gameguild.emception.workspace.user-1:assessment-1.cpp.v2');
    expect(props.workspaceConfig.files).toEqual({ 'main.cpp': { encoding: 'text', content: '// starter' } });
    expect(props.assignmentToken).toBeUndefined();
    expect(props.testPlan).toBeUndefined();
    expect(screen.getByRole('button', { name: /^Submit$/ })).toBeEnabled();
  });

  it('submits the current session delta without recompiling the browser workspace', async () => {
    const delta = [{ path: 'main.cpp', content: 'int main(){}' }];
    const session = stubAssessmentEditor(delta);
    mocks.getCodingAssignmentPublic.mockResolvedValue(makeAssignment());
    mocks.submitRuntimeSubmission.mockResolvedValue({ success: true, data: {} });

    render(await LearnerActivityPage(pageParams()));

    await screen.findByTestId('mock-assessment-editor');
    fireEvent.click(screen.getByRole('button', { name: /^Submit$/ }));
    await waitFor(() => expect(mocks.submitRuntimeSubmission).toHaveBeenCalledTimes(1));
    expect(session.run).not.toHaveBeenCalled();
    expect(session.getSubmissionDelta).toHaveBeenCalledTimes(1);
    expect(mocks.submitRuntimeSubmission).toHaveBeenCalledWith('official-submission-1', {
      schemaVersion: 1, contentType: 'coding-assignment', payloadSchema: 'code-files/v1',
      payload: { files: { 'main.cpp': { content: 'int main(){}', encoding: 'text' } } },
    }, expect.any(String));
    expect(mocks.submitAssessment).not.toHaveBeenCalled();
    expect(mocks.useRouterPush).toHaveBeenCalledWith('/learn/courses/test-course/activities');
  });

  it('passes only public files to the workspace template even if a malformed learner response contains private data', async () => {
    const malformedDelivery = makeAssignment({
      Data: {
        Files: {
          'main.cpp': { Content: 'public', Encoding: 'text', Visibility: 'Public', Modifiable: true },
          'private-fixture.cpp': { Content: 'secret', Encoding: 'text', Visibility: 'Private', Modifiable: false },
        },
      },
    });
    mocks.getCodingAssignmentPublic.mockResolvedValue(malformedDelivery);
    useOfficialDefinition(malformedDelivery);

    render(await LearnerActivityPage(pageParams()));

    const editor = await screen.findByTestId('mock-assessment-editor');
    const props = JSON.parse(editor.dataset.props ?? '{}');
    expect(props.workspaceConfig.files).toEqual({ 'main.cpp': { encoding: 'text', content: 'public' } });
    expect(JSON.stringify(props.workspaceConfig.files)).not.toContain('secret');
  });

  it('renders the public estimate from an assessment run result', async () => {
    mocks.computeScore.mockReturnValue({ score: 100, passed: true, feedback: '' });
    const publicTests = makeAssignment({
      Tests: {
        Public: [
          { kind: 'standard', Name: 't1', Weight: 2, Stdout: 'x' },
          { kind: 'standard', Name: 't2', Weight: 3, Stdout: 'y' },
        ],
        Private: [],
      },
    });
    mocks.getCodingAssignmentPublic.mockResolvedValue(publicTests);
    useOfficialDefinition(publicTests);

    render(await LearnerActivityPage(pageParams()));
    await screen.findByTestId('mock-assessment-editor');
    const onRunResult = mocks.EditorRender.mock.calls.at(-1)?.[0]?.onRunResult as (result: unknown) => void;
    await act(async () => {
      onRunResult({
        report: {
          passed: 2, failed: 0, totalDurationMs: 0,
          cases: [{ name: 't1', passed: true, durationMs: 0 }, { name: 't2', passed: true, durationMs: 0 }],
        },
      });
    });

    const banner = await screen.findByTestId('public-test-estimate-banner');
    expect(banner).toHaveTextContent('2/2 passed');
    expect(banner).toHaveTextContent('estimated score: 100/100');
  });

  it('uses the frozen official delivery even when current authoring has changed', async () => {
    mocks.getCodingAssignmentPublic.mockResolvedValue(makeAssignment({
      Data: { Files: { 'main.cpp': { Content: 'new mutable authoring', Encoding: 'text', Visibility: 'Public' } } },
    }));
    useOfficialDefinition(makeAssignment());

    render(await LearnerActivityPage(pageParams()));

    const editor = await screen.findByTestId('mock-assessment-editor');
    const props = JSON.parse(editor.dataset.props ?? '{}');
    expect(props.workspaceConfig.files).toEqual({ 'main.cpp': { encoding: 'text', content: '// starter' } });
    expect(JSON.stringify(props.workspaceConfig.files)).not.toContain('new mutable authoring');
  });

  it('falls back to the ordinary learner form when there is no valid coding assignment', async () => {
    mocks.getCodingAssignmentPublic.mockResolvedValue(null);

    render(await LearnerActivityPage(pageParams()));

    expect(screen.queryByTestId('mock-assessment-editor')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Submit assessment/i })).toBeInTheDocument();
  });

  it('does not fetch or render a coding editor for a non-code assessment', async () => {
    mocks.getCourseLearnerContext.mockResolvedValue(makeContext(makeAssessment({
      type: 'Quiz', submissionModalities: 'StructuredAnswer',
    })));

    render(await LearnerActivityPage(pageParams()));

    expect(mocks.getCodingAssignmentPublic).not.toHaveBeenCalled();
    expect(screen.queryByTestId('mock-assessment-editor')).not.toBeInTheDocument();
  });
});
