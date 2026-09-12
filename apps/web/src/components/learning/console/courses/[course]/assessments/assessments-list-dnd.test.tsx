import '@testing-library/jest-dom/vitest';
import { act, render, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AssessmentsList } from './assessments-list';
import { updateAssessment } from '@/lib/learning/actions';
import type { Assessment } from '@/lib/learning/queries/assessments';

const dndHarness = vi.hoisted(() => ({
  handlers: [] as Array<(event: { active: { id: string }; over: { id: string } | null }) => void>,
  contextIds: [] as Array<string | undefined>,
}));

const routerMocks = vi.hoisted(() => ({
  refresh: vi.fn(),
}));

vi.mock('@dnd-kit/core', () => ({
  DndContext: ({
    children,
    id,
    onDragEnd,
  }: {
    children: ReactNode;
    id?: string;
    onDragEnd?: (event: { active: { id: string }; over: { id: string } | null }) => void;
  }) => {
    dndHarness.contextIds.push(id);
    if (onDragEnd) dndHarness.handlers.push(onDragEnd);
    return children;
  },
  DragOverlay: ({ children }: { children: ReactNode }) => children ?? null,
  PointerSensor: vi.fn(),
  closestCorners: vi.fn(),
  useDraggable: () => ({
    attributes: {},
    listeners: {},
    setNodeRef: vi.fn(),
    transform: null,
    isDragging: false,
  }),
  useDroppable: () => ({
    setNodeRef: vi.fn(),
    isOver: false,
  }),
  useSensor: vi.fn(() => ({})),
  useSensors: vi.fn(() => []),
}));

vi.mock('@dnd-kit/utilities', () => ({
  CSS: {
    Translate: {
      toString: () => undefined,
    },
  },
}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({ href, children }: { href: string; children: ReactNode }) => <a href={href}>{children}</a>,
  usePathname: () => '/workspace/learning/courses/course-1/assessments',
  useRouter: () => ({ refresh: routerMocks.refresh }),
}));

vi.mock('@/lib/learning/actions', () => ({
  createAssessment: vi.fn(),
  createAssessmentGroup: vi.fn(),
  deleteAssessmentGroup: vi.fn(),
  updateAssessment: vi.fn(),
  updateAssessmentGroup: vi.fn(),
}));

Object.defineProperties(HTMLElement.prototype, {
  hasPointerCapture: { value: vi.fn(() => false) },
  setPointerCapture: { value: vi.fn() },
  releasePointerCapture: { value: vi.fn() },
  scrollIntoView: { value: vi.fn() },
});

global.ResizeObserver = class ResizeObserver {
  observe() {}
  unobserve() {}
  disconnect() {}
};

const assessmentGroups = [
  { id: 'group-a', courseId: 'course-1', name: 'Group A', description: null, weightPercent: 50, order: 1 },
  { id: 'group-b', courseId: 'course-1', name: 'Group B', description: null, weightPercent: 50, order: 2 },
  { id: 'group-feedback', courseId: 'course-1', name: 'Feedback', description: null, weightPercent: 0, order: 3 },
];

const unassignedAssessment = {
  id: 'assessment-unassigned',
  courseId: 'course-1',
  contentId: null,
  title: 'Unassigned quiz',
  description: null,
  type: 'Quiz',
  maxScore: 10,
  passingScore: 7,
  timeLimitMinutes: null,
  maxAttempts: null,
  isRequired: true,
  order: 1,
  availableFrom: null,
  availableUntil: null,
  isAvailable: true,
  assessmentGroupId: null,
  assessmentGroupName: null,
  assessmentGroupWeightPercent: null,
  assessmentGroupOrder: null,
} as unknown as Assessment;

const groupAAssessment = {
  ...unassignedAssessment,
  id: 'assessment-groupA',
  title: 'In group A',
  assessmentGroupId: 'group-a',
  assessmentGroupName: 'Group A',
  assessmentGroupWeightPercent: 50,
  assessmentGroupOrder: 1,
} as unknown as Assessment;

const groupBAssessment = {
  ...unassignedAssessment,
  id: 'assessment-groupB',
  title: 'In group B',
  assessmentGroupId: 'group-b',
  assessmentGroupName: 'Group B',
  assessmentGroupWeightPercent: 50,
  assessmentGroupOrder: 2,
} as unknown as Assessment;

const feedbackAssessment = {
  ...unassignedAssessment,
  id: 'assessment-feedback',
  title: 'Practice quiz',
  assessmentGroupId: 'group-feedback',
  assessmentGroupName: 'Feedback',
  assessmentGroupWeightPercent: 0,
  assessmentGroupOrder: 3,
} as unknown as Assessment;

const assessments = [unassignedAssessment, groupAAssessment, groupBAssessment, feedbackAssessment];

function renderList() {
  dndHarness.handlers.length = 0;
  dndHarness.contextIds.length = 0;
  routerMocks.refresh.mockClear();
  return render(
    <AssessmentsList
      courseId="course-1"
      assessments={assessments}
      total={assessments.length}
      assessmentGroups={assessmentGroups}
    />,
  );
}

describe('AssessmentsList DnD between grade groups', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(updateAssessment).mockResolvedValue({ success: true, data: null });
  });

  it('uses a deterministic context id for server and client rendering', () => {
    renderList();

    expect(dndHarness.contextIds).toEqual(['assessments-course-1']);
  });

  it('moves an unassigned assessment into Group A via drop', async () => {
    renderList();
    const dragEnd = dndHarness.handlers[0]!;

    await act(async () => {
      dragEnd({ active: { id: 'assessment-assessment-unassigned' }, over: { id: 'group-drop-group-a' } });
    });

    await waitFor(() => {
      expect(updateAssessment).toHaveBeenCalledWith({
        courseId: 'course-1',
        assessmentId: 'assessment-unassigned',
        assessmentGroupId: 'group-a',
      });
    });
    expect(routerMocks.refresh).toHaveBeenCalled();
  });

  it('moves an assessment from Group A to Group B', async () => {
    renderList();
    const dragEnd = dndHarness.handlers[0]!;

    await act(async () => {
      dragEnd({ active: { id: 'assessment-assessment-groupA' }, over: { id: 'group-drop-group-b' } });
    });

    await waitFor(() => {
      expect(updateAssessment).toHaveBeenCalledWith({
        courseId: 'course-1',
        assessmentId: 'assessment-groupA',
        assessmentGroupId: 'group-b',
      });
    });
  });

  it('moves a feedback assessment into a weighted gradebook group', async () => {
    renderList();
    const dragEnd = dndHarness.handlers[0]!;

    await act(async () => {
      dragEnd({ active: { id: 'assessment-assessment-feedback' }, over: { id: 'group-drop-group-a' } });
    });

    await waitFor(() => {
      expect(updateAssessment).toHaveBeenCalledWith({
        courseId: 'course-1',
        assessmentId: 'assessment-feedback',
        assessmentGroupId: 'group-a',
      });
    });
  });

  it('clears the group when dragging into the Unassigned section', async () => {
    renderList();
    const dragEnd = dndHarness.handlers[0]!;

    await act(async () => {
      dragEnd({ active: { id: 'assessment-assessment-groupA' }, over: { id: 'group-drop-ungrouped' } });
    });

    await waitFor(() => {
      expect(updateAssessment).toHaveBeenCalledWith({
        courseId: 'course-1',
        assessmentId: 'assessment-groupA',
        clearAssessmentGroupId: true,
      });
    });
  });

  it('does not call updateAssessment when dropped outside a droppable target', async () => {
    renderList();
    const dragEnd = dndHarness.handlers[0]!;

    await act(async () => {
      dragEnd({ active: { id: 'assessment-assessment-groupA' }, over: null });
    });

    expect(updateAssessment).not.toHaveBeenCalled();
    expect(routerMocks.refresh).not.toHaveBeenCalled();
  });

  it('does not call updateAssessment when dropped on the same group', async () => {
    renderList();
    const dragEnd = dndHarness.handlers[0]!;

    await act(async () => {
      dragEnd({ active: { id: 'assessment-assessment-groupA' }, over: { id: 'group-drop-group-a' } });
    });

    expect(updateAssessment).not.toHaveBeenCalled();
  });

  it('adopts the destination group when dropped on another assessment card', async () => {
    renderList();
    const dragEnd = dndHarness.handlers[0]!;

    await act(async () => {
      dragEnd({ active: { id: 'assessment-assessment-unassigned' }, over: { id: 'assessment-assessment-groupB' } });
    });

    await waitFor(() => {
      expect(updateAssessment).toHaveBeenCalledWith({
        courseId: 'course-1',
        assessmentId: 'assessment-unassigned',
        assessmentGroupId: 'group-b',
      });
    });
  });

  it('reverts and surfaces an error when the move fails', async () => {
    vi.mocked(updateAssessment).mockResolvedValueOnce({ success: false, error: 'Cannot move locked assessment.' });
    renderList();
    const dragEnd = dndHarness.handlers[0]!;

    await act(async () => {
      dragEnd({ active: { id: 'assessment-assessment-groupA' }, over: { id: 'group-drop-group-b' } });
    });

    await waitFor(() => {
      expect(updateAssessment).toHaveBeenCalled();
    });
    expect(routerMocks.refresh).not.toHaveBeenCalled();
  });
});
