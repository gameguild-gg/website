'use client';

import type { DragEndEvent, DragStartEvent } from '@dnd-kit/core';
import {
  closestCorners,
  DndContext,
  DragOverlay,
  PointerSensor,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
} from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import { Link, usePathname, useRouter } from '@/i18n/navigation';
import { createAssessment, createAssessmentGroup, deleteAssessmentGroup, updateAssessment, updateAssessmentGroup } from '@/lib/learning/actions';
import type { Assessment, AssessmentGroup, AssessmentType, CourseAssessmentAnalytics } from '@/lib/learning/queries/assessments';
import { normalizeSlug, slugify } from '@/lib/slugify';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Card, CardContent } from '@game-guild/ui/components/card';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@game-guild/ui/components/dialog';
import { Input } from '@game-guild/ui/components/input';
import { Label } from '@game-guild/ui/components/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@game-guild/ui/components/select';
import { AlertTriangle, BarChart3, ChevronDown, ClipboardCheck, ClipboardList, GripVertical, Loader2, Pencil, Plus, Target, Trash2, Trophy, Wand2 } from 'lucide-react';
import React, { useState, useTransition } from 'react';

function typeIcon(type: AssessmentType) {
  switch (type) {
    case 'Quiz':
      return <ClipboardList className="size-4" />;
    case 'Project':
      return <Trophy className="size-4" />;
    default:
      return <Target className="size-4" />;
  }
}

function typeBadgeVariant(type: AssessmentType): 'default' | 'secondary' | 'outline' {
  switch (type) {
    case 'Quiz':
      return 'secondary';
    default:
      return 'outline';
  }
}

const CREATE_ASSESSMENT_TYPES: AssessmentType[] = ['Quiz', 'Assignment', 'Project'];
type AssessmentGradingMethodFlag = 'PeerReview' | 'AIGraded' | 'AutoGraded' | 'InstructorGraded';
const GRADING_METHOD_FLAGS: AssessmentGradingMethodFlag[] = [
  'PeerReview',
  'AIGraded',
  'AutoGraded',
  'InstructorGraded',
];
const NO_GROUP_VALUE = '__none__';
const UNGROUPED_ID = 'ungrouped';
const UNGROUPED_NAME = 'Unassigned';
const UNGROUPED_DESCRIPTION = 'Activities that still need a grading group.';
const ASSESSMENT_DRAG_PREFIX = 'assessment-';
const GROUP_DROP_PREFIX = 'group-drop-';

interface AssessmentsListProps {
  courseId: string;
  assessments: Assessment[];
  total: number;
  assessmentGroups?: AssessmentGroup[];
  analytics?: CourseAssessmentAnalytics | null;
  canManage?: boolean;
}

interface AssessmentGroupView {
  id: string;
  name: string;
  description: string | null;
  weightPercent: number | null;
  order: number;
  assessments: Assessment[];
}

function formatWeight(weightPercent: number | null) {
  if (weightPercent == null) return 'Unconfigured';
  return `${formatPercent(weightPercent)} of Total`;
}

function formatAssessmentRole(weightPercent: number | null) {
  if (weightPercent == null) return 'Unconfigured';
  return weightPercent === 0 ? 'Practice' : 'Gradebook';
}

function formatPercent(value: number) {
  return `${Number.isInteger(value) ? value : value.toFixed(1)}%`;
}

function buildGroupedAssessments(assessments: Assessment[], assessmentGroups: AssessmentGroup[]): AssessmentGroupView[] {
  const groups = new Map<string, AssessmentGroupView>();

  // Seed an Unassigned slot only when there is something to drag.
  if (assessments.length > 0 || assessmentGroups.length > 0) {
    groups.set(UNGROUPED_ID, {
      id: UNGROUPED_ID,
      name: UNGROUPED_NAME,
      description: UNGROUPED_DESCRIPTION,
      weightPercent: null,
      order: Number.MAX_SAFE_INTEGER,
      assessments: [],
    });
  }

  for (const group of assessmentGroups) {
    groups.set(group.id, {
      id: group.id,
      name: group.name,
      description: group.description,
      weightPercent: group.weightPercent,
      order: group.order,
      assessments: [],
    });
  }

  for (const assessment of assessments) {
    const groupId = assessment.assessmentGroupId ?? UNGROUPED_ID;
    // ponytail: orphan FK (group deleted) falls back to Unassigned instead of
    // fabricating a phantom group entry. One slot, one fallback path.
    const targetId = groups.has(groupId) ? groupId : UNGROUPED_ID;
    groups.get(targetId)!.assessments.push(assessment);
  }

  return [...groups.values()]
    .sort((a, b) => a.order - b.order || a.name.localeCompare(b.name))
    .map((group) => ({
      ...group,
      assessments: [...group.assessments].sort((a, b) => a.order - b.order || a.title.localeCompare(b.title)),
    }));
}

function DraggableAssessmentRow({ id, children }: { id: string; children: React.ReactNode }) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({ id });
  const style: React.CSSProperties = {
    transform: CSS.Translate.toString(transform),
    opacity: isDragging ? 0.4 : undefined,
  };
  return (
    <div ref={setNodeRef} style={style} {...listeners} {...attributes}>
      {children}
    </div>
  );
}

function DroppableGroupBody({ id, children }: { id: string; children: React.ReactNode }) {
  const { setNodeRef, isOver } = useDroppable({ id });
  return (
    <div ref={setNodeRef} className={isOver ? 'bg-primary/5 transition-colors' : undefined}>
      {children}
    </div>
  );
}

function AssessmentAnalyticsPanel({ analytics }: { analytics: CourseAssessmentAnalytics }) {
  const maxBucketCount = Math.max(1, ...analytics.distribution.map((bucket) => bucket.count));

  return (
    <Card>
      <CardContent className="space-y-5 p-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h3 className="flex items-center gap-2 text-base font-semibold">
              <BarChart3 className="size-4" aria-hidden="true" />
              Score distribution
            </h3>
            <p className="text-muted-foreground mt-1 text-sm">
              Assessment outcomes by score bucket and weighted grade group.
            </p>
          </div>
          <div className="grid grid-cols-3 gap-2 text-right text-sm">
            <div>
              <p className="text-muted-foreground text-xs">Average</p>
              <p className="font-semibold">{formatPercent(analytics.averagePercent)}</p>
            </div>
            <div>
              <p className="text-muted-foreground text-xs">Pass rate</p>
              <p className="font-semibold">{formatPercent(analytics.passRate)}</p>
            </div>
            <div>
              <p className="text-muted-foreground text-xs">Grading</p>
              <p className="font-semibold">{analytics.gradedCount} graded</p>
            </div>
          </div>
        </div>

        {analytics.gradedCount === 0 ? (
          <div className="rounded-lg border border-dashed p-5 text-center">
            <p className="text-sm font-medium">No graded scores yet</p>
            <p className="text-muted-foreground mt-1 text-xs">
              Score distribution appears after submissions are graded.
            </p>
          </div>
        ) : (
          <div className="grid gap-3 md:grid-cols-5">
            {analytics.distribution.map((bucket) => (
              <div key={bucket.label} className="rounded-lg border bg-muted/20 p-3">
                <div className="mb-2 flex items-center justify-between text-xs">
                  <span className="font-medium">{bucket.label}</span>
                  <span className="text-muted-foreground">{bucket.count}</span>
                </div>
                <div className="h-2 overflow-hidden rounded-full bg-muted">
                  <div
                    className="h-full rounded-full bg-primary"
                    style={{ width: `${Math.max(6, (bucket.count / maxBucketCount) * 100)}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        )}

        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {analytics.groups.map((group) => (
            <div key={group.groupId ?? group.groupName} className="rounded-lg border p-3">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <p className="text-sm font-semibold">{group.groupName}</p>
                  <p className="text-muted-foreground text-xs">
                    {group.gradedCount} graded · {group.ungradedCount} ungraded
                  </p>
                </div>
                <Badge variant="outline">{group.weightPercent == null ? 'Unconfigured' : formatPercent(group.weightPercent)}</Badge>
              </div>
              <div className="mt-3 grid grid-cols-2 gap-2 text-sm">
                <div>
                  <p className="text-muted-foreground text-xs">Average</p>
                  <p className="font-semibold">{formatPercent(group.averagePercent)}</p>
                </div>
                <div>
                  <p className="text-muted-foreground text-xs">Pass rate</p>
                  <p className="font-semibold">{formatPercent(group.passRate)}</p>
                </div>
              </div>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

export function AssessmentsList({
  courseId,
  assessments,
  total,
  assessmentGroups = [],
  analytics = null,
  canManage = false,
}: AssessmentsListProps) {
  const router = useRouter();
  const pathname = usePathname();
  const [showCreateGroup, setShowCreateGroup] = useState(false);
  const [isGroupPending, startGroupTransition] = useTransition();

  const [newGroupName, setNewGroupName] = useState('');
  const [newGroupWeight, setNewGroupWeight] = useState('20');
  const [groupError, setGroupError] = useState<string | null>(null);
  const [editingGroup, setEditingGroup] = useState<AssessmentGroup | null>(null);
  const [editGroupName, setEditGroupName] = useState('');
  const [editGroupDescription, setEditGroupDescription] = useState('');
  const [editGroupWeight, setEditGroupWeight] = useState('');
  const [editGroupError, setEditGroupError] = useState<string | null>(null);
  const [deletingGroup, setDeletingGroup] = useState<AssessmentGroup | null>(null);
  const [deleteGroupError, setDeleteGroupError] = useState<string | null>(null);

  const [showCreateAssessment, setShowCreateAssessment] = useState(false);
  const [newAssessmentTitle, setNewAssessmentTitle] = useState('');
  const [newAssessmentSlug, setNewAssessmentSlug] = useState('');
  const [newAssessmentAutoSlug, setNewAssessmentAutoSlug] = useState(true);
  const [newAssessmentType, setNewAssessmentType] = useState<AssessmentType>('Assignment');
  const [newAssessmentGroupId, setNewAssessmentGroupId] = useState<string>(NO_GROUP_VALUE);
  const [newAssessmentGradingMethods, setNewAssessmentGradingMethods] = useState<Set<AssessmentGradingMethodFlag>>(
    () => new Set<AssessmentGradingMethodFlag>(['InstructorGraded']),
  );
  const [assessmentError, setAssessmentError] = useState<string | null>(null);
  const [isAssessmentPending, startAssessmentTransition] = useTransition();

  const [activeAssessmentId, setActiveAssessmentId] = useState<string | null>(null);
  const [moveError, setMoveError] = useState<string | null>(null);
  const [isMovePending, startMoveTransition] = useTransition();
  const dndSensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
  );

  const groupedAssessments = React.useMemo(
    () => buildGroupedAssessments(assessments, assessmentGroups),
    [assessments, assessmentGroups],
  );
  const weightTotal = React.useMemo(
    () => assessmentGroups.reduce((sum, group) => sum + group.weightPercent, 0),
    [assessmentGroups],
  );
  const hasWeightWarning = assessmentGroups.length > 0 && Math.round(weightTotal * 100) / 100 !== 100;

  function handleCreateGroup() {
    if (!newGroupName.trim()) {
      setGroupError('Group name is required.');
      return;
    }

    const weight = Number(newGroupWeight);
    if (!Number.isFinite(weight) || weight < 0 || weight > 100) {
      setGroupError('Weight must be between 0 and 100.');
      return;
    }

    setGroupError(null);
    startGroupTransition(async () => {
      const result = await createAssessmentGroup({
        courseId,
        name: newGroupName.trim(),
        weightPercent: weight,
        order: assessmentGroups.length + 1,
      });

      if (result.success) {
        setShowCreateGroup(false);
        setNewGroupName('');
        setNewGroupWeight('20');
        router.refresh();
      } else {
        setGroupError(result.error);
      }
    });
  }

  function openEditGroup(group: AssessmentGroupView) {
    if (group.id === 'ungrouped' || group.weightPercent == null) return;

    const source = assessmentGroups.find((item) => item.id === group.id) ?? {
      id: group.id,
      courseId,
      name: group.name,
      description: group.description,
      weightPercent: group.weightPercent,
      order: group.order,
    };

    setEditingGroup(source);
    setEditGroupName(source.name);
    setEditGroupDescription(source.description ?? '');
    setEditGroupWeight(String(source.weightPercent));
    setEditGroupError(null);
  }

  function handleUpdateGroup() {
    if (!editingGroup) return;

    if (!editGroupName.trim()) {
      setEditGroupError('Group name is required.');
      return;
    }

    const weight = Number(editGroupWeight);
    if (!Number.isFinite(weight) || weight < 0 || weight > 100) {
      setEditGroupError('Weight must be between 0 and 100.');
      return;
    }

    setEditGroupError(null);
    startGroupTransition(async () => {
      const result = await updateAssessmentGroup({
        courseId,
        groupId: editingGroup.id,
        name: editGroupName.trim(),
        description: editGroupDescription.trim() || null,
        weightPercent: weight,
        order: editingGroup.order,
      });

      if (result.success) {
        setEditingGroup(null);
        router.refresh();
      } else {
        setEditGroupError(result.error);
      }
    });
  }

  function openDeleteGroup(group: AssessmentGroupView) {
    if (group.id === 'ungrouped' || group.weightPercent == null) return;

    const source = assessmentGroups.find((item) => item.id === group.id) ?? {
      id: group.id,
      courseId,
      name: group.name,
      description: group.description,
      weightPercent: group.weightPercent,
      order: group.order,
    };

    setDeletingGroup(source);
    setDeleteGroupError(null);
  }

  function handleDeleteGroup() {
    if (!deletingGroup) return;

    setDeleteGroupError(null);
    startGroupTransition(async () => {
      const result = await deleteAssessmentGroup(courseId, deletingGroup.id);

      if (result.success) {
        setDeletingGroup(null);
        router.refresh();
      } else {
        setDeleteGroupError(result.error);
      }
    });
  }

  function toggleGradingMethod(method: AssessmentGradingMethodFlag) {
    setNewAssessmentGradingMethods((prev) => {
      const next = new Set(prev);
      if (next.has(method)) {
        next.delete(method);
      } else {
        next.add(method);
      }
      return next;
    });
  }

  function handleNewAssessmentTitleChange(value: string) {
    setNewAssessmentTitle(value);
    if (newAssessmentAutoSlug) {
      setNewAssessmentSlug(slugify(value));
    }
  }

  function handleNewAssessmentSlugChange(value: string) {
    setNewAssessmentAutoSlug(false);
    setNewAssessmentSlug(slugify(value));
  }

  function resetCreateAssessmentForm() {
    setNewAssessmentTitle('');
    setNewAssessmentSlug('');
    setNewAssessmentAutoSlug(true);
    setNewAssessmentType('Assignment');
    setNewAssessmentGroupId(NO_GROUP_VALUE);
    setNewAssessmentGradingMethods(new Set<AssessmentGradingMethodFlag>(['InstructorGraded']));
    setAssessmentError(null);
  }

  function handleCreateAssessment() {
    const trimmedTitle = newAssessmentTitle.trim();
    if (!trimmedTitle) {
      setAssessmentError('Title is required.');
      return;
    }
    if (newAssessmentGradingMethods.size === 0) {
      setAssessmentError('Select at least one grading method.');
      return;
    }

    setAssessmentError(null);
    startAssessmentTransition(async () => {
      const result = await createAssessment({
        courseId,
        title: trimmedTitle,
        ...(normalizeSlug(newAssessmentSlug) ? { slug: normalizeSlug(newAssessmentSlug) } : {}),
        type: newAssessmentType,
        assessmentGroupId: newAssessmentGroupId === NO_GROUP_VALUE ? null : newAssessmentGroupId,
        gradingMethods: [...newAssessmentGradingMethods].join(','),
      });

      if (result.success) {
        setShowCreateAssessment(false);
        resetCreateAssessmentForm();
        router.refresh();
      } else {
        setAssessmentError(result.error);
      }
    });
  }

  function handleDragStart(event: DragStartEvent) {
    setActiveAssessmentId(String(event.active.id));
  }

  function handleDragEnd(event: DragEndEvent) {
    setActiveAssessmentId(null);
    const { active, over } = event;
    if (!over) return;

    const activeId = String(active.id);
    const overId = String(over.id);
    if (activeId === overId) return;
    if (!activeId.startsWith(ASSESSMENT_DRAG_PREFIX)) return;

    const assessmentId = activeId.slice(ASSESSMENT_DRAG_PREFIX.length);
    const assessment = assessments.find((a) => a.id === assessmentId);
    if (!assessment) return;
    const currentGroupId = assessment.assessmentGroupId ?? UNGROUPED_ID;

    let targetGroupId: string;
    if (overId.startsWith(GROUP_DROP_PREFIX)) {
      targetGroupId = overId.slice(GROUP_DROP_PREFIX.length);
    } else if (overId.startsWith(ASSESSMENT_DRAG_PREFIX)) {
      const overAssessmentId = overId.slice(ASSESSMENT_DRAG_PREFIX.length);
      const overAssessment = assessments.find((a) => a.id === overAssessmentId);
      if (!overAssessment) return;
      targetGroupId = overAssessment.assessmentGroupId ?? UNGROUPED_ID;
    } else {
      return;
    }

    if (targetGroupId === currentGroupId) return;

    setMoveError(null);
    startMoveTransition(async () => {
      const result = targetGroupId === UNGROUPED_ID
        ? await updateAssessment({ courseId, assessmentId, clearAssessmentGroupId: true })
        : await updateAssessment({ courseId, assessmentId, assessmentGroupId: targetGroupId });

      if (result.success) {
        router.refresh();
      } else {
        setMoveError(result.error);
      }
    });
  }

  return (
    <div className="flex flex-col gap-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <h2 className="text-lg font-semibold">Assessments</h2>
          <Badge variant="secondary">{total}</Badge>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button size="sm" onClick={() => setShowCreateAssessment(true)}>
            <Wand2 className="mr-2 h-4 w-4" />
            Create Assessment
          </Button>
          <Button size="sm" variant="outline" onClick={() => setShowCreateGroup(true)}>
            <Plus className="mr-2 h-4 w-4" />
            Add Group
          </Button>
        </div>
      </div>

      {hasWeightWarning && (
        <Card className="border-amber-500/50 bg-amber-500/10">
          <CardContent className="flex items-start gap-3 p-4">
            <AlertTriangle className="mt-0.5 size-4 shrink-0 text-amber-500" aria-hidden="true" />
            <div>
              <p className="text-sm font-semibold">Grade weights total {formatPercent(weightTotal)}.</p>
              <p className="text-muted-foreground text-sm">Adjust groups until they equal 100%.</p>
            </div>
          </CardContent>
        </Card>
      )}

      {analytics && <AssessmentAnalyticsPanel analytics={analytics} />}

      {/* Empty state */}
      {total === 0 && assessmentGroups.length === 0 && (
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12 text-center">
            <ClipboardList className="text-muted-foreground mb-4 size-12" />
            <h3 className="text-lg font-medium">No assessments yet</h3>
            <p className="text-muted-foreground mt-1 text-sm">
              Graded content will appear here after grading is enabled from the content editor.
            </p>
          </CardContent>
        </Card>
      )}

      {/* Weighted grade groups */}
      {groupedAssessments.length > 0 && (
        <DndContext
          id={`assessments-${courseId}`}
          sensors={dndSensors}
          collisionDetection={closestCorners}
          onDragStart={handleDragStart}
          onDragEnd={handleDragEnd}
          onDragCancel={() => setActiveAssessmentId(null)}
        >
          {moveError && (
            <div role="alert" className="text-destructive text-sm">
              {moveError}
            </div>
          )}
          {isMovePending && (
            <div className="text-muted-foreground text-sm">Moving assessment…</div>
          )}
          <div className="overflow-hidden rounded-xl border bg-card">
            {groupedAssessments.map((group) => (
              <section key={group.id} data-testid={`assessment-group-${group.id}`} className="border-b last:border-b-0">
                <div className="flex min-h-12 items-center gap-3 bg-muted/60 px-4 py-3">
                  <GripVertical className="text-muted-foreground size-4 shrink-0" aria-hidden="true" />
                  <ChevronDown className="text-muted-foreground size-4 shrink-0" aria-hidden="true" />
                  <div className="min-w-0 flex-1">
                    <h3 className="truncate text-sm font-semibold">{group.name}</h3>
                    {group.description && <p className="text-muted-foreground mt-0.5 truncate text-xs">{group.description}</p>}
                  </div>
                  <Badge variant="outline" className="shrink-0 rounded-full bg-background">
                    {formatWeight(group.weightPercent)}
                  </Badge>
                  <Badge variant="outline">{formatAssessmentRole(group.weightPercent)}</Badge>
                  {group.id !== UNGROUPED_ID && (
                    <>
                      <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        className="size-8 p-0"
                        aria-label={`Edit group ${group.name}`}
                        onClick={() => openEditGroup(group)}
                      >
                        <Pencil className="size-4" aria-hidden="true" />
                      </Button>
                      <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        className="size-8 p-0 text-destructive hover:text-destructive"
                        aria-label={`Delete group ${group.name}`}
                        onClick={() => openDeleteGroup(group)}
                      >
                        <Trash2 className="size-4" aria-hidden="true" />
                      </Button>
                    </>
                  )}
                </div>

                <DroppableGroupBody id={`${GROUP_DROP_PREFIX}${group.id}`}>
                  <div className="divide-y">
                    {group.assessments.map((assessment) => (
                      <DraggableAssessmentRow key={assessment.id} id={`${ASSESSMENT_DRAG_PREFIX}${assessment.id}`}>
                        <Link
                          href={`${pathname}/${assessment.id}`}
                          className="group flex min-h-16 items-center gap-3 px-4 py-3 transition hover:bg-muted/45"
                        >
                          <GripVertical className="text-muted-foreground/70 size-4 shrink-0 cursor-grab" aria-hidden="true" />
                          <span className="bg-muted text-muted-foreground flex size-9 shrink-0 items-center justify-center rounded-md">
                            {typeIcon(assessment.type)}
                          </span>
                          <span className="min-w-0 flex-1">
                            <span className="block truncate text-sm font-semibold underline-offset-2 group-hover:underline">
                              {assessment.title}
                            </span>
                            <span className="text-muted-foreground mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
                              {assessment.timeLimitMinutes && <span>{assessment.timeLimitMinutes}m</span>}
                              {assessment.maxAttempts && <span>{assessment.maxAttempts} attempts</span>}
                              <span>{assessment.maxScore} pts</span>
                            </span>
                          </span>
                          <Badge variant={typeBadgeVariant(assessment.type)} className="hidden shrink-0 sm:inline-flex">
                            {assessment.type}
                          </Badge>
                          <Badge variant={assessment.isAvailable ? 'secondary' : 'outline'} className="hidden shrink-0 sm:inline-flex">
                            {assessment.isAvailable ? 'available' : 'scheduled'}
                          </Badge>
                        </Link>
                        {canManage && (
                          <Button asChild variant="outline" size="sm" className="mr-4 shrink-0">
                            <Link
                              href={`${pathname}/${assessment.id}/submissions`}
                              data-testid={`grade-link-${assessment.id}`}
                            >
                              <ClipboardCheck className="mr-2 h-4 w-4" />
                              Grade
                            </Link>
                          </Button>
                        )}
                      </DraggableAssessmentRow>
                    ))}
                  </div>
                </DroppableGroupBody>
              </section>
            ))}
          </div>
          <DragOverlay dropAnimation={null}>
            {activeAssessmentId
              ? (() => {
                  const previewId = activeAssessmentId.slice(ASSESSMENT_DRAG_PREFIX.length);
                  const preview = assessments.find((a) => a.id === previewId);
                  return preview ? (
                    <div className="bg-card flex items-center gap-3 rounded-md border px-4 py-3 shadow-md">
                      <span className="bg-muted text-muted-foreground flex size-9 shrink-0 items-center justify-center rounded-md">
                        {typeIcon(preview.type)}
                      </span>
                      <span className="text-sm font-semibold">{preview.title}</span>
                    </div>
                  ) : null;
                })()
              : null}
          </DragOverlay>
        </DndContext>
      )}

      {/* Create Assessment Group Dialog */}
      <Dialog open={showCreateGroup} onOpenChange={setShowCreateGroup}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create Assessment Group</DialogTitle>
            <DialogDescription>
              Group graded activities into weighted blocks such as quizzes, midterms, projects, or attendance.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-2">
            <div className="space-y-2">
              <Label htmlFor="assessment-group-name">Group name</Label>
              <Input
                id="assessment-group-name"
                value={newGroupName}
                onChange={(e) => setNewGroupName(e.target.value)}
                placeholder="e.g. Final Project"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="assessment-group-weight">Weight percent</Label>
              <Input
                id="assessment-group-weight"
                type="number"
                min={0}
                max={100}
                value={newGroupWeight}
                onChange={(e) => setNewGroupWeight(e.target.value)}
              />
            </div>
            {groupError && <p className="text-destructive text-sm">{groupError}</p>}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setShowCreateGroup(false)}>
              Cancel
            </Button>
            <Button onClick={handleCreateGroup} disabled={isGroupPending}>
              {isGroupPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Create Group
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Edit Assessment Group Dialog */}
      <Dialog open={Boolean(editingGroup)} onOpenChange={(open) => !open && setEditingGroup(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Edit Assessment Group</DialogTitle>
            <DialogDescription>
              Update the weighted grading block used to calculate course outcomes.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-2">
            <div className="space-y-2">
              <Label htmlFor="edit-assessment-group-name">Group name</Label>
              <Input
                id="edit-assessment-group-name"
                value={editGroupName}
                onChange={(e) => setEditGroupName(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-assessment-group-description">Description</Label>
              <Input
                id="edit-assessment-group-description"
                value={editGroupDescription}
                onChange={(e) => setEditGroupDescription(e.target.value)}
                placeholder="Optional grading guidance"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-assessment-group-weight">Weight percent</Label>
              <Input
                id="edit-assessment-group-weight"
                type="number"
                min={0}
                max={100}
                value={editGroupWeight}
                onChange={(e) => setEditGroupWeight(e.target.value)}
              />
            </div>
            {editGroupError && <p className="text-destructive text-sm">{editGroupError}</p>}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditingGroup(null)}>
              Cancel
            </Button>
            <Button onClick={handleUpdateGroup} disabled={isGroupPending}>
              {isGroupPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save Group
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete Assessment Group Dialog */}
      <Dialog open={Boolean(deletingGroup)} onOpenChange={(open) => !open && setDeletingGroup(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete Assessment Group</DialogTitle>
            <DialogDescription>
              This removes "{deletingGroup?.name}" and will move existing assessments to ungrouped work.
            </DialogDescription>
          </DialogHeader>

          {deleteGroupError && <p className="text-destructive text-sm">{deleteGroupError}</p>}

          <DialogFooter>
            <Button variant="outline" onClick={() => setDeletingGroup(null)}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={handleDeleteGroup} disabled={isGroupPending}>
              {isGroupPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Delete Group
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Create Assessment Dialog */}
      <Dialog
        open={showCreateAssessment}
        onOpenChange={(open) => {
          setShowCreateAssessment(open);
          if (!open) resetCreateAssessmentForm();
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create Assessment</DialogTitle>
            <DialogDescription>
              Add a standalone graded activity. Link it to course content later from the editor.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-2">
            <div className="space-y-2">
              <Label htmlFor="new-assessment-title">Title</Label>
              <Input
                id="new-assessment-title"
                value={newAssessmentTitle}
                onChange={(e) => handleNewAssessmentTitleChange(e.target.value)}
                placeholder="e.g. Midterm Exam"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="new-assessment-slug">URL Slug (optional)</Label>
              <Input
                id="new-assessment-slug"
                value={newAssessmentSlug}
                onChange={(e) => handleNewAssessmentSlugChange(e.target.value)}
                onBlur={() => setNewAssessmentSlug(normalizeSlug(newAssessmentSlug))}
                placeholder="midterm-exam"
              />
              <p className="text-muted-foreground text-xs">
                Auto-generated from title. Edit to customize.
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="new-assessment-type">Type</Label>
              <Select value={newAssessmentType} onValueChange={(value) => setNewAssessmentType(value as AssessmentType)}>
                <SelectTrigger id="new-assessment-type">
                  <SelectValue placeholder="Choose a type" />
                </SelectTrigger>
                <SelectContent>
                  {CREATE_ASSESSMENT_TYPES.map((type) => (
                    <SelectItem key={type} value={type}>
                      {type}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="new-assessment-group">Grade group</Label>
              <Select value={newAssessmentGroupId} onValueChange={setNewAssessmentGroupId}>
                <SelectTrigger id="new-assessment-group">
                  <SelectValue placeholder="Unassigned" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NO_GROUP_VALUE}>Unassigned</SelectItem>
                  {assessmentGroups.map((group) => (
                    <SelectItem key={group.id} value={group.id}>
                      {group.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Grading methods</Label>
              <div className="grid grid-cols-2 gap-2">
                {GRADING_METHOD_FLAGS.map((method) => {
                  const checked = newAssessmentGradingMethods.has(method);
                  return (
                    <Label
                      key={method}
                      htmlFor={`grading-method-${method}`}
                      className={`flex cursor-pointer items-center gap-2 rounded-md border px-3 py-2 text-sm transition ${
                        checked ? 'border-primary bg-primary/5' : 'hover:bg-muted/40'
                      }`}
                    >
                      <input
                        id={`grading-method-${method}`}
                        type="checkbox"
                        className="size-4 accent-primary"
                        checked={checked}
                        onChange={() => toggleGradingMethod(method)}
                      />
                      {method}
                    </Label>
                  );
                })}
              </div>
            </div>

            {assessmentError && <p className="text-destructive text-sm">{assessmentError}</p>}
          </div>

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setShowCreateAssessment(false);
                resetCreateAssessmentForm();
              }}
            >
              Cancel
            </Button>
            <Button
              onClick={handleCreateAssessment}
              disabled={!newAssessmentTitle.trim() || isAssessmentPending}
            >
              {isAssessmentPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Create Assessment
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

    </div>
  );
}
