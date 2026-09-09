"use client";

import React, { useEffect, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@game-guild/ui/components/card";
import { Badge } from "@game-guild/ui/components/badge";
import { Button } from "@game-guild/ui/components/button";
import { Input } from "@game-guild/ui/components/input";
import { Label } from "@game-guild/ui/components/label";
import { Textarea } from "@game-guild/ui/components/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@game-guild/ui/components/select";
import { Switch } from "@game-guild/ui/components/switch";
import { Separator } from "@game-guild/ui/components/separator";
import {
  ArrowLeft,
  CheckCircle2,
  ClipboardCheck,
  Clock,
  Code,
  FileCheck2,
  Gauge,
  Loader2,
  Plus,
  RotateCcw,
  Save,
  Trash2,
  Upload,
  X,
} from "lucide-react";
import type {
  Assessment,
  AssessmentAuthoringState,
  AssessmentGroup,
  AssessmentPresentationMode,
  AssessmentType,
} from "@/lib/learning/queries/assessments";
import {
  ASSESSMENT_PRIMARY_REVIEW_METHODS,
  buildReviewWorkflow,
  readReviewWorkflow,
  REVIEW_METHOD_LABELS,
  type AssessmentReviewMethod,
} from "@/lib/learning/assessment-grading-methods";
import type { CourseContentItemViewModel } from "@/lib/learning/queries/course";
import { Link } from "@/i18n/navigation";
import {
  deleteAssessment,
  deleteRubric,
  prepareAssessmentRevision,
  publishAssessmentRevision,
  saveRubric,
  unpublishAssessmentRevision,
  updateAssessment,
} from "@/lib/learning/actions";
import { useLearningBase } from '@/lib/learning/use-learning-base';
import { normalizeSlug, slugify } from "@/lib/slugify";

const ASSESSMENT_TYPE_OPTIONS: { value: AssessmentType; label: string }[] = [
  { value: "Quiz", label: "Quiz" },
  { value: "Assignment", label: "Assignment" },
  { value: "Project", label: "Project" },
  { value: "PeerReview", label: "Peer Review" },
  { value: "SelfAssessment", label: "Self Assessment" },
];

const GROUP_SET_NONE = "none";

const RUBRIC_LOCK_MESSAGE = "Rubric locked after grading started";

const DEFAULT_PEER_REVIEWS = 3;

function readPeerReviewsRequired(configuration: string | null): number {
  if (!configuration) return DEFAULT_PEER_REVIEWS;
  try {
    const parsed = JSON.parse(configuration) as {
      peer?: { reviewsRequiredPerSubmission?: unknown } | null;
    };
    const value = parsed.peer?.reviewsRequiredPerSubmission;
    return typeof value === "number" && Number.isInteger(value) && value > 0
      ? value
      : DEFAULT_PEER_REVIEWS;
  } catch {
    return DEFAULT_PEER_REVIEWS;
  }
}

function buildReviewConfiguration(
  current: string | null,
  primary: AssessmentReviewMethod,
  requiresInstructorReview: boolean,
  peerReviewsRequired: number,
): string {
  let parsed: Record<string, unknown> = {};
  try {
    parsed = current ? (JSON.parse(current) as Record<string, unknown>) : {};
  } catch {
    parsed = {};
  }

  const instructorSelected =
    primary === "InstructorReview" || requiresInstructorReview;
  return JSON.stringify({
    schemaVersion: 1,
    peer:
      primary === "PeerReview"
        ? {
            ...((parsed.peer as Record<string, unknown> | null) ?? {}),
            reviewsPerReviewer: peerReviewsRequired,
            reviewsRequiredPerSubmission: peerReviewsRequired,
            minimumReviewsToFinalize: peerReviewsRequired,
            aggregation: "mean",
            claimLeaseMinutes: 30,
            evidenceWindowMinutes: 10080,
            onInsufficientEvidence: "await-instructor-resolution",
          }
        : null,
    ai:
      primary === "AIReview"
        ? (parsed.ai ?? { providerKey: "unconfigured", policyVersion: "1" })
        : null,
    self:
      primary === "SelfReview"
        ? (parsed.self ?? { instructions: null, requireFeedback: true })
        : null,
    instructor: instructorSelected
      ? (parsed.instructor ?? { requireOverrideReason: false })
      : null,
  });
}

export interface GroupSetOption {
  id: string;
  name: string;
}

export interface RubricCriterionRow {
  description: string;
  points: number | null;
}

export interface RubricViewModel {
  title: string;
  criteria: { description: string; points: number; order: number }[];
}

interface AssessmentEditorProps {
  courseId: string;
  assessment: Assessment;
  authoringState?: AssessmentAuthoringState | null;
  assessmentGroups?: AssessmentGroup[];
  courseContent?: CourseContentItemViewModel[];
  groupSets?: GroupSetOption[];
  rubric?: RubricViewModel | null;
  rubricLocked?: boolean;
  canManage?: boolean;
}

function formatWeight(weightPercent: number) {
  return `${Number.isInteger(weightPercent) ? weightPercent : weightPercent.toFixed(1)}% of Total`;
}

export function AssessmentEditor({
  courseId,
  assessment,
  authoringState = null,
  assessmentGroups = [],
  courseContent = [],
  groupSets = [],
  rubric = null,
  rubricLocked = false,
  canManage = false,
}: AssessmentEditorProps) {
  const learningBase = useLearningBase();
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [isDeleting, startDeleteTransition] = useTransition();
  const [isPolicyPending, startPolicyTransition] = useTransition();
  const [isRubricPending, startRubricTransition] = useTransition();
  const [isLifecyclePending, startLifecycleTransition] = useTransition();
  const isQuiz = assessment.type === "Quiz";
  const isLinkedQuiz = isQuiz && assessment.contentId != null;
  const [assessmentVersion, setAssessmentVersion] = useState(assessment.version);

  useEffect(() => {
    setAssessmentVersion(assessment.version);
  }, [assessment.version]);

  const [title, setTitle] = useState(assessment.title);
  const [slug, setSlug] = useState(assessment.slug);
  // Slug starts in auto mode regardless of the stored value (it may be a
  // legacy backfill): title edits regenerate it until the slug is edited
  // directly in this session, which detaches it.
  const [autoSlug, setAutoSlug] = useState(true);
  const [description, setDescription] = useState(assessment.description ?? "");
  const [maxScore, setMaxScore] = useState(String(assessment.maxScore));
  const [passingScore, setPassingScore] = useState(
    String(assessment.passingScore),
  );
  const [timeLimitMinutes, setTimeLimitMinutes] = useState(
    assessment.timeLimitMinutes != null
      ? String(assessment.timeLimitMinutes)
      : "",
  );
  const [maxAttempts, setMaxAttempts] = useState(
    assessment.maxAttempts != null ? String(assessment.maxAttempts) : "",
  );
  const [isRequired, setIsRequired] = useState(assessment.isRequired);
  const [assessmentGroupId, setAssessmentGroupId] = useState(
    assessment.assessmentGroupId ?? "none",
  );
  const [presentationMode, setPresentationMode] =
    useState<AssessmentPresentationMode>(assessment.presentationMode);
  const [availableFrom, setAvailableFrom] = useState(
    assessment.availableFrom ? assessment.availableFrom.slice(0, 16) : "",
  );
  const [availableUntil, setAvailableUntil] = useState(
    assessment.availableUntil ? assessment.availableUntil.slice(0, 16) : "",
  );
  const initialReviewWorkflow = readReviewWorkflow(assessment.reviewMethods);
  const [primaryReviewMethod, setPrimaryReviewMethod] =
    useState<AssessmentReviewMethod>(
      initialReviewWorkflow.primary ?? "InstructorReview",
    );
  const [requiresInstructorReview, setRequiresInstructorReview] = useState(
    initialReviewWorkflow.requiresInstructorReview,
  );
  const [contentCompletionMode, setContentCompletionMode] = useState(
    assessment.contentCompletionMode,
  );
  const [resultReleaseMode, setResultReleaseMode] = useState(
    assessment.resultReleaseMode,
  );
  const [groupSetId, setGroupSetId] = useState(
    assessment.groupSetId ?? GROUP_SET_NONE,
  );
  const [groupAssignmentOn, setGroupAssignmentOn] = useState(
    assessment.groupSetId != null,
  );
  const [peerReviewsRequired, setPeerReviewsRequired] = useState(
    String(
      readPeerReviewsRequired(assessment.reviewConfigurationCanonicalJson),
    ),
  );
  const [rubricOn, setRubricOn] = useState(rubric != null);
  const [rubricTitle] = useState(rubric?.title ?? "Rubric");
  const [criteria, setCriteria] = useState<RubricCriterionRow[]>(() =>
    rubric != null && rubric.criteria.length > 0
      ? rubric.criteria.map((criterion) => ({
          description: criterion.description,
          points: criterion.points,
        }))
      : [{ description: "", points: null }],
  );
  const [rubricLockedLocal, setRubricLockedLocal] = useState(rubricLocked);
  const [rubricSaved, setRubricSaved] = useState(false);

  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  function handleTitleChange(value: string) {
    setTitle(value);
    if (autoSlug) {
      setSlug(slugify(value));
    }
  }

  function handleSlugChange(value: string) {
    setAutoSlug(false);
    setSlug(slugify(value));
  }

  function handleSave() {
    if (!title.trim()) {
      setError("Title is required.");
      return;
    }
    setError(null);
    setSaved(false);

    startTransition(async () => {
      const reviewMethods = buildReviewWorkflow(
        primaryReviewMethod,
        requiresInstructorReview,
      );
      const requiredPeerReviews = Math.max(
        1,
        Number(peerReviewsRequired) || DEFAULT_PEER_REVIEWS,
      );
      const result = await updateAssessment({
        courseId,
        assessmentId: assessment.id,
        expectedVersion: assessmentVersion,
        ...(isLinkedQuiz
          ? {}
          : {
              title: title.trim(),
              slug: normalizeSlug(slug) || normalizeSlug(title),
              description: description.trim() || null,
            }),
        maxScore:
          isLinkedQuiz
            ? undefined
            : maxScore === ""
              ? undefined
              : Number(maxScore),
        passingScore:
          passingScore === "" ? undefined : Number(passingScore),
        timeLimitMinutes: timeLimitMinutes ? Number(timeLimitMinutes) : null,
        maxAttempts: maxAttempts ? Number(maxAttempts) : null,
        isRequired,
        availableFrom: availableFrom || null,
        availableUntil: availableUntil || null,
        assessmentGroupId:
          assessmentGroupId === "none" ? null : assessmentGroupId,
        clearAssessmentGroupId: assessmentGroupId === "none",
        presentationMode,
        reviewMethods,
        reviewConfigurationCanonicalJson: buildReviewConfiguration(
          assessment.reviewConfigurationCanonicalJson,
          primaryReviewMethod,
          requiresInstructorReview,
          requiredPeerReviews,
        ),
        contentCompletionMode,
        resultReleaseMode,
      });

      if (!result.success) {
        setError(result.error);
        return;
      }

      setAssessmentVersion(result.data.version);
      setSaved(true);

      // The editor route resolves by slug or id — after a slug change the
      // current URL is stale, so replace it instead of refreshing in place.
      const savedSlug = normalizeSlug(slug) || normalizeSlug(title);
      if (!isLinkedQuiz && savedSlug && savedSlug !== assessment.slug) {
        router.replace(
          `${learningBase}/courses/${encodeURIComponent(courseId)}/assessments/${savedSlug}`,
        );
      } else {
        router.refresh();
      }
    });
  }

  function handleDelete() {
    if (!confirm("Are you sure you want to delete this assessment?")) return;

    startDeleteTransition(async () => {
      const result = await deleteAssessment(courseId, assessment.id);
      if (result.success) {
        router.push(
          `${learningBase}/courses/${encodeURIComponent(courseId)}/assessments`,
        );
      } else {
        setError(result.error);
      }
    });
  }

  function handleBack() {
    router.push(
      `${learningBase}/courses/${encodeURIComponent(courseId)}/assessments`,
    );
  }

  function handleGroupAssignmentToggle(checked: boolean) {
    setGroupAssignmentOn(checked);
    setError(null);

    if (checked || groupSetId === GROUP_SET_NONE) {
      return;
    }

    const previous = groupSetId;
    setGroupSetId(GROUP_SET_NONE);

    startPolicyTransition(async () => {
      const result = await updateAssessment({
        courseId,
        assessmentId: assessment.id,
        expectedVersion: assessmentVersion,
        groupSetId: null,
        clearGroupSetId: true,
      });

      if (!result.success) {
        setGroupSetId(previous);
        setGroupAssignmentOn(true);
        setError(result.error);
        return;
      }

      setAssessmentVersion(result.data.version);
      router.refresh();
    });
  }

  function handleGroupSetChange(value: string) {
    const previous = groupSetId;
    setGroupSetId(value);
    setError(null);

    startPolicyTransition(async () => {
      const result = await updateAssessment({
        courseId,
        assessmentId: assessment.id,
        expectedVersion: assessmentVersion,
        groupSetId: value === GROUP_SET_NONE ? null : value,
        clearGroupSetId: value === GROUP_SET_NONE,
      });

      if (!result.success) {
        setGroupSetId(previous);
        setError(result.error);
        return;
      }

      setAssessmentVersion(result.data.version);
      router.refresh();
    });
  }

  const rubricLockedNow = rubricLockedLocal;
  const criteriaPointsSum = criteria.reduce(
    (sum, row) => sum + (row.points ?? 0),
    0,
  );
  const criteriaValid =
    criteria.length > 0 &&
    criteria.every(
      (row) => row.description.trim() !== "" && row.points != null && row.points > 0,
    );
  const rubricSumMatches = criteriaPointsSum === assessment.maxScore;
  const canSaveRubric = criteriaValid && rubricSumMatches && !rubricLockedNow;

  function addCriterionRow() {
    setCriteria((rows) => [...rows, { description: "", points: null }]);
  }

  function removeCriterionRow(index: number) {
    setCriteria((rows) =>
      rows.length > 1
        ? rows.filter((_, rowIndex) => rowIndex !== index)
        : [{ description: "", points: null }],
    );
  }

  function handleSaveRubric() {
    setError(null);
    setRubricSaved(false);

    startRubricTransition(async () => {
      const result = await saveRubric({
        assessmentId: assessment.id,
        title: rubricTitle,
        criteria: criteria.map((row, index) => ({
          description: row.description.trim(),
          points: row.points ?? 0,
          order: index,
        })),
      });

      if (!result.success) {
        if (result.error.includes(RUBRIC_LOCK_MESSAGE)) {
          setRubricLockedLocal(true);
          setError(null);
          return;
        }
        setError(result.error);
        return;
      }

      setRubricSaved(true);
      router.refresh();
    });
  }

  function handleDeleteRubric() {
    if (!confirm("Are you sure you want to remove the rubric from this assessment?")) {
      return;
    }

    startRubricTransition(async () => {
      const result = await deleteRubric(assessment.id);

      if (!result.success) {
        if (result.error.includes(RUBRIC_LOCK_MESSAGE)) {
          setRubricLockedLocal(true);
          setError(null);
          return;
        }
        setError(result.error);
        return;
      }

      setRubricOn(false);
      setCriteria([{ description: "", points: null }]);
      router.refresh();
    });
  }

  function handlePrepareRevision() {
    setError(null);
    startLifecycleTransition(async () => {
      const result = await prepareAssessmentRevision(
        courseId,
        assessment.id,
        assessmentVersion,
      );
      if (!result.success) {
        setError(result.error);
        return;
      }
      router.refresh();
    });
  }

  function handlePublishRevision() {
    const candidate = authoringState?.candidate;
    if (!candidate) return;

    setError(null);
    startLifecycleTransition(async () => {
      const result = await publishAssessmentRevision(
        courseId,
        assessment.id,
        candidate.revisionId,
        assessmentVersion,
      );
      if (!result.success) {
        setError(result.error);
        return;
      }
      setAssessmentVersion((version) => version + 1);
      router.refresh();
    });
  }

  function handleUnpublishRevision() {
    const published = authoringState?.published;
    if (!published) return;

    setError(null);
    startLifecycleTransition(async () => {
      const result = await unpublishAssessmentRevision(
        courseId,
        assessment.id,
        published.revisionId,
        assessmentVersion,
        crypto.randomUUID(),
      );
      if (!result.success) {
        setError(result.error);
        return;
      }
      setAssessmentVersion((version) => version + 1);
      router.refresh();
    });
  }

  const typeLabel =
    ASSESSMENT_TYPE_OPTIONS.find((o) => o.value === assessment.type)?.label ??
    assessment.type;
  const selectedAssessmentGroup = assessmentGroups.find(
    (group) => group.id === assessmentGroupId,
  );
  const assessmentRole = !selectedAssessmentGroup
    ? "Unconfigured"
    : selectedAssessmentGroup.weightPercent === 0
      ? "Practice"
      : "Gradebook";

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="sm" onClick={handleBack}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Back
        </Button>
        <div className="flex-1">
          <p className="text-muted-foreground text-sm">Assessment Editor</p>
          <h1 className="text-2xl font-bold">{assessment.title}</h1>
        </div>
        {canManage && (
          <Button variant="outline" size="sm" asChild>
            <Link
              href={`/speedgrader/assessments/${assessment.id}?course=${encodeURIComponent(courseId)}`}
              data-testid="start-speedgrader-button"
            >
              <Gauge className="mr-2 h-4 w-4" />
              SpeedGrader
            </Link>
          </Button>
        )}
        {canManage && (
          <Button variant="outline" size="sm" asChild>
            <Link
              href={`${learningBase}/courses/${encodeURIComponent(courseId)}/assessments/${assessment.slug}/submissions`}
              data-testid="grade-submissions-button"
            >
              <ClipboardCheck className="mr-2 h-4 w-4" />
              Grade submissions
            </Link>
          </Button>
        )}
        <Badge variant="secondary">{typeLabel}</Badge>
        <Badge variant={assessmentRole === "Gradebook" ? "default" : "outline"}>
          {assessmentRole}
        </Badge>
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        {/* Main editor */}
        <div className="space-y-6 lg:col-span-2">
          <Card>
            <CardHeader>
              <CardTitle>Details</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="title">Title</Label>
                <Input
                  id="title"
                  value={title}
                  onChange={(e) => handleTitleChange(e.target.value)}
                  placeholder="Assessment title"
                  disabled={isLinkedQuiz}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="slug">URL Slug</Label>
                <Input
                  id="slug"
                  value={slug}
                  onChange={(e) => handleSlugChange(e.target.value)}
                  onBlur={() => setSlug(normalizeSlug(slug))}
                  placeholder="midterm-exam"
                  disabled={isLinkedQuiz}
                />
                <p className="text-muted-foreground text-xs">
                  Auto-generated from title. Edit to customize.
                </p>
              </div>

              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Instructions or description for students"
                  rows={4}
                  disabled={isLinkedQuiz}
                />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Scoring</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="max-score">Max Score</Label>
                  <Input
                    id="max-score"
                    type="number"
                    min={1}
                    value={maxScore}
                    onChange={(e) => setMaxScore(e.target.value)}
                    disabled={isLinkedQuiz}
                  />
                  {isQuiz && assessment.contentId != null && (
                    <p className="text-muted-foreground text-xs">
                      Calculated from the quiz question points.
                    </p>
                  )}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="passing-score">Passing Score</Label>
                  <Input
                    id="passing-score"
                    type="number"
                    min={0}
                    value={passingScore}
                    onChange={(e) => setPassingScore(e.target.value)}
                  />
                </div>
              </div>
              <p className="text-muted-foreground text-xs">
                Students need at least {passingScore || 0} out of{" "}
                {maxScore || 0} points to pass (
                {maxScore && Number(maxScore) > 0
                  ? Math.round(
                      (Number(passingScore || 0) / Number(maxScore)) * 100,
                    )
                  : 0}
                %).
              </p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <CardTitle>Rubric</CardTitle>
                <div className="flex items-center gap-2">
                  <Label
                    htmlFor="grade-by-rubric"
                    className="text-sm font-normal text-muted-foreground"
                  >
                    Grade by rubric
                  </Label>
                  <Switch
                    id="grade-by-rubric"
                    checked={rubricOn}
                    onCheckedChange={(checked) => setRubricOn(checked === true)}
                    disabled={rubricLockedNow}
                  />
                </div>
              </div>
            </CardHeader>
            {rubricOn && (
              <CardContent className="space-y-4">
                {rubricLockedNow && (
                  <p className="text-destructive text-sm" role="alert">
                    {RUBRIC_LOCK_MESSAGE}
                  </p>
                )}
                <div className="space-y-3">
                  {criteria.map((row, index) => (
                    <div
                      key={index}
                      className="flex items-end gap-2"
                    >
                          <div className="flex-1 space-y-1">
                            <Label htmlFor={`criterion-${index + 1}-description`}>
                              Criterion {index + 1} description
                            </Label>
                            <Input
                              id={`criterion-${index + 1}-description`}
                              value={row.description}
                              onChange={(e) =>
                                setCriteria((rows) =>
                                  rows.map((current, rowIndex) =>
                                    rowIndex === index
                                      ? { ...current, description: e.target.value }
                                      : current,
                                  ),
                                )
                              }
                              placeholder="What this criterion assesses"
                              disabled={isRubricPending || rubricLockedNow}
                            />
                          </div>
                          <div className="w-24 space-y-1">
                            <Label htmlFor={`criterion-${index + 1}-points`}>
                              Criterion {index + 1} points
                            </Label>
                            <Input
                              id={`criterion-${index + 1}-points`}
                              type="number"
                              min={0.01}
                              step={0.01}
                              value={row.points ?? ""}
                              onChange={(e) =>
                                setCriteria((rows) =>
                                  rows.map((current, rowIndex) =>
                                    rowIndex === index
                                      ? {
                                          ...current,
                                          points: e.target.value
                                            ? Number(e.target.value)
                                            : null,
                                        }
                                      : current,
                                  ),
                                )
                              }
                              disabled={isRubricPending || rubricLockedNow}
                            />
                          </div>
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            aria-label={`Remove criterion ${index + 1}`}
                            onClick={() => removeCriterionRow(index)}
                            disabled={isRubricPending}
                          >
                            <X className="size-4" />
                          </Button>
                        </div>
                      ))}
                    </div>

                    <div className="flex items-center justify-between">
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={addCriterionRow}
                        disabled={isRubricPending}
                      >
                        <Plus className="mr-2 size-4" />
                        Add criterion
                      </Button>
                      <p
                        data-testid="rubric-sum"
                        className={`text-sm font-semibold ${
                          rubricSumMatches
                            ? "text-green-600"
                            : "text-destructive"
                        }`}
                      >
                        Σ {criteriaPointsSum} / {assessment.maxScore}
                        {!rubricSumMatches && (
                          <span>
                            {" "}
                            ({criteriaPointsSum > assessment.maxScore ? "+" : ""}
                            {criteriaPointsSum - assessment.maxScore})
                          </span>
                        )}
                      </p>
                    </div>

                    <div className="flex items-center gap-2">
                      <Button
                        type="button"
                        onClick={handleSaveRubric}
                        disabled={!canSaveRubric || isRubricPending}
                      >
                        {isRubricPending ? (
                          <Loader2 className="mr-2 size-4 animate-spin" />
                        ) : (
                          <Save className="mr-2 size-4" />
                        )}
                        Save rubric
                      </Button>
                      <Button
                        type="button"
                        variant="outline"
                        onClick={handleDeleteRubric}
                        disabled={isRubricPending}
                      >
                        <Trash2 className="mr-2 size-4" />
                        Delete rubric
                      </Button>
                      {rubricSaved && (
                        <span className="text-sm text-green-600">
                          Rubric saved.
                        </span>
                      )}
                    </div>
                    <p className="text-muted-foreground text-xs">
                      Criterion points must sum to the assessment max score.
                      Locked after grading starts.
                    </p>
              </CardContent>
            )}
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Availability</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="available-from">Available From</Label>
                  <Input
                    id="available-from"
                    type="datetime-local"
                    value={availableFrom}
                    onChange={(e) => setAvailableFrom(e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="available-until">Available Until</Label>
                  <Input
                    id="available-until"
                    type="datetime-local"
                    value={availableUntil}
                    onChange={(e) => setAvailableUntil(e.target.value)}
                  />
                </div>
              </div>
              <p className="text-muted-foreground text-xs">
                Leave empty to make the assessment always available.
              </p>
            </CardContent>
          </Card>
        </div>

        {/* Sidebar */}
        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Settings</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <Label>Type</Label>
                <Select value={assessment.type} disabled>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {ASSESSMENT_TYPE_OPTIONS.map((opt) => (
                      <SelectItem key={opt.value} value={opt.value}>
                        {opt.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-muted-foreground text-xs">
                  Type cannot be changed after creation.
                </p>
              </div>

              <Separator />

              <div className="space-y-2">
                <Label htmlFor="grade-group">Grading group</Label>
                <Select
                  value={assessmentGroupId}
                  onValueChange={setAssessmentGroupId}
                >
                  <SelectTrigger id="grade-group">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No group</SelectItem>
                    {assessmentGroups.map((group) => (
                      <SelectItem key={group.id} value={group.id}>
                        {group.name} ({formatWeight(group.weightPercent)})
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-muted-foreground text-xs">
                  Zero-weight groups are practice activities. Positive-weight groups contribute to the gradebook.
                </p>
              </div>

              <div className="space-y-2">
                <Label>Assessment role</Label>
                <Input value={assessmentRole} readOnly className="bg-muted" />
              </div>

              <Separator />

              <div className="space-y-2">
                <Label>Linked content</Label>
                {(() => {
                  const item = courseContent.find(
                    (c) => c.id === assessment.contentId,
                  );
                  return assessment.contentId != null ? (
                    <div className="space-y-1">
                      <Link
                        href={`${learningBase}/courses/${encodeURIComponent(courseId)}/content/${item?.slug ?? assessment.contentId}`}
                        className="text-sm text-primary underline-offset-4 hover:underline"
                        data-testid="linked-content-link"
                      >
                        {item?.title ?? assessment.contentId}
                      </Link>
                      <p className="text-muted-foreground text-xs">
                        This assessment was created by its content and cannot
                        be unlinked.
                      </p>
                    </div>
                  ) : (
                    <p className="text-muted-foreground text-sm" data-testid="linked-content-none">
                      Not linked (standalone assessment).
                    </p>
                  );
                })()}
              </div>

              <Separator />

              {isLinkedQuiz && authoringState && (
                <>
                  <div
                    className="space-y-3"
                    data-testid="assessment-publication-state"
                  >
                    <div className="flex items-center justify-between gap-3">
                      <Label>Revision lifecycle</Label>
                      <Badge
                        variant={
                          authoringState.lifecycle === "published"
                            ? "default"
                            : authoringState.lifecycle === "changes-pending"
                              ? "destructive"
                              : "outline"
                        }
                      >
                        {authoringState.lifecycle === "draft" && "Draft"}
                        {authoringState.lifecycle === "candidate" &&
                          "Candidate"}
                        {authoringState.lifecycle === "published" &&
                          "Published"}
                        {authoringState.lifecycle === "changes-pending" &&
                          "Changes pending"}
                      </Badge>
                    </div>

                    <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-1">
                      <div className="rounded-md border p-3">
                        <div className="flex items-center gap-2 text-sm font-medium">
                          <FileCheck2 className="size-4" aria-hidden="true" />
                          Candidate
                        </div>
                        <p className="text-muted-foreground mt-1 text-xs">
                          {authoringState.candidate
                            ? `Revision ${authoringState.candidate.revisionNumber}${
                                authoringState.candidateMatchesDraft
                                  ? " matches the draft"
                                  : " is outdated"
                              }.`
                            : "No candidate prepared."}
                        </p>
                      </div>
                      <div className="rounded-md border p-3">
                        <div className="flex items-center gap-2 text-sm font-medium">
                          <CheckCircle2 className="size-4" aria-hidden="true" />
                          Active
                        </div>
                        <p className="text-muted-foreground mt-1 text-xs">
                          {authoringState.published
                            ? `Revision ${authoringState.published.revisionNumber}${
                                authoringState.publishedMatchesDraft
                                  ? " matches the draft"
                                  : " has unpublished changes"
                              }.`
                            : "No active revision."}
                        </p>
                      </div>
                    </div>

                    <div className="flex flex-wrap gap-2">
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={handlePrepareRevision}
                        disabled={
                          isLifecyclePending ||
                          !authoringState.prepare.available
                        }
                      >
                        {isLifecyclePending ? (
                          <Loader2 className="mr-2 size-4 animate-spin" />
                        ) : (
                          <FileCheck2 className="mr-2 size-4" />
                        )}
                        Prepare
                      </Button>
                      <Button
                        type="button"
                        size="sm"
                        onClick={handlePublishRevision}
                        disabled={
                          isLifecyclePending ||
                          !authoringState.publish.available ||
                          !authoringState.candidateMatchesDraft
                        }
                      >
                        <Upload className="mr-2 size-4" />
                        Publish
                      </Button>
                      {authoringState.published && (
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          onClick={handleUnpublishRevision}
                          disabled={isLifecyclePending}
                        >
                          <RotateCcw className="mr-2 size-4" />
                          Unpublish
                        </Button>
                      )}
                    </div>

                    {!authoringState.prepare.available &&
                      authoringState.prepare.message && (
                        <p className="text-muted-foreground text-xs" role="status">
                          Test capability: {authoringState.prepare.message}
                        </p>
                      )}
                    {authoringState.candidateMatchesDraft &&
                      !authoringState.publish.available &&
                      authoringState.publish.message && (
                        <p className="text-muted-foreground text-xs" role="status">
                          Official capability: {authoringState.publish.message}
                        </p>
                      )}
                  </div>
                  <Separator />
                </>
              )}

              <div className="space-y-3">
                <div className="space-y-2">
                  <Label htmlFor="primary-review-method">Primary review</Label>
                  <Select
                    value={primaryReviewMethod}
                    onValueChange={(value) => {
                      const method = value as AssessmentReviewMethod;
                      setPrimaryReviewMethod(method);
                      if (method === "InstructorReview") {
                        setRequiresInstructorReview(false);
                      }
                    }}
                  >
                    <SelectTrigger id="primary-review-method">
                      <SelectValue>
                        {(value) =>
                          REVIEW_METHOD_LABELS[
                            value as AssessmentReviewMethod
                          ] ?? "Select review method"
                        }
                      </SelectValue>
                    </SelectTrigger>
                    <SelectContent>
                      {ASSESSMENT_PRIMARY_REVIEW_METHODS.map((method) => (
                        <SelectItem key={method} value={method}>
                          {REVIEW_METHOD_LABELS[method]}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {primaryReviewMethod !== "InstructorReview" && (
                  <div className="flex items-start justify-between gap-3 rounded-md border p-3">
                    <div>
                      <Label htmlFor="instructor-final-review">
                        Final instructor review
                      </Label>
                      <p className="text-muted-foreground mt-1 text-xs">
                        The primary reviewer proposes the result; an instructor
                        performs the final review.
                      </p>
                    </div>
                    <Switch
                      id="instructor-final-review"
                      checked={requiresInstructorReview}
                      onCheckedChange={(checked) =>
                        setRequiresInstructorReview(checked === true)
                      }
                    />
                  </div>
                )}

                <p className="text-muted-foreground text-xs">
                  Sequence: {REVIEW_METHOD_LABELS[primaryReviewMethod]}
                  {requiresInstructorReview &&
                  primaryReviewMethod !== "InstructorReview"
                    ? ` -> ${REVIEW_METHOD_LABELS.InstructorReview}`
                    : ""}
                </p>
              </div>

              <Separator />

              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <Label htmlFor="group-assignment">Group assignment</Label>
                  <Switch
                    id="group-assignment"
                    checked={groupAssignmentOn}
                    onCheckedChange={handleGroupAssignmentToggle}
                    disabled={isPolicyPending}
                  />
                </div>
                {groupAssignmentOn && (
                  <div className="space-y-2">
                    <Label htmlFor="group-set">Group set</Label>
                    <Select
                      value={groupSetId}
                      onValueChange={handleGroupSetChange}
                      disabled={isPolicyPending}
                    >
                      <SelectTrigger id="group-set">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value={GROUP_SET_NONE}>
                          No group set
                        </SelectItem>
                        {groupSets.map((set) => (
                          <SelectItem key={set.id} value={set.id}>
                            {set.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <p className="text-muted-foreground text-xs">
                      Students submit once per group; the grade applies to every
                      member.
                    </p>
                  </div>
                )}
              </div>

              <Separator />

              {primaryReviewMethod === "PeerReview" && (
                <>
                  <div className="space-y-2">
                    <Label htmlFor="required-reviews">Required peer reviews</Label>
                    <Input
                      id="required-reviews"
                      type="number"
                      min={1}
                      value={peerReviewsRequired}
                      onChange={(e) => setPeerReviewsRequired(e.target.value)}
                    />
                    <p className="text-muted-foreground text-xs">
                      Each student reviews this many submissions from other
                      students.
                    </p>
                  </div>
                  <Separator />
                </>
              )}

              <div className="space-y-2">
                <Label htmlFor="result-release-mode">Result release</Label>
                <Select
                  value={resultReleaseMode}
                  onValueChange={setResultReleaseMode}
                >
                  <SelectTrigger id="result-release-mode">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="immediate">Immediate</SelectItem>
                    <SelectItem value="manual">Manual</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label htmlFor="content-completion-mode">Content completion</Label>
                <Select
                  value={contentCompletionMode}
                  onValueChange={setContentCompletionMode}
                >
                  <SelectTrigger id="content-completion-mode">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="on-submit">On submission</SelectItem>
                    <SelectItem value="on-finalize">On final result</SelectItem>
                    <SelectItem value="on-release">On result release</SelectItem>
                    <SelectItem value="on-release-and-pass">
                      On released passing result
                    </SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <Separator />

              {isQuiz && (
                <>
                  <div className="space-y-2">
                    <Label htmlFor="presentation-mode">Presentation</Label>
                    <Select
                      value={presentationMode}
                      onValueChange={(value) =>
                        setPresentationMode(value as AssessmentPresentationMode)
                      }
                    >
                      <SelectTrigger id="presentation-mode">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Continuous">
                          Continuous list
                        </SelectItem>
                        <SelectItem value="SingleStep">
                          One at a time
                        </SelectItem>
                      </SelectContent>
                    </Select>
                  </div>

                  <Separator />
                </>
              )}

              <div className="space-y-2">
                <Label htmlFor="time-limit">
                  <Clock className="mr-1 inline h-3 w-3" />
                  Time Limit (minutes)
                </Label>
                <Input
                  id="time-limit"
                  type="number"
                  min={0}
                  value={timeLimitMinutes}
                  onChange={(e) => setTimeLimitMinutes(e.target.value)}
                  placeholder="No limit"
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="max-attempts">Max Attempts</Label>
                <Input
                  id="max-attempts"
                  type="number"
                  min={1}
                  value={maxAttempts}
                  onChange={(e) => setMaxAttempts(e.target.value)}
                  placeholder="1"
                  disabled
                />
              </div>

              <div className="flex items-center justify-between">
                <Label htmlFor="required">Required</Label>
                <Switch
                  id="required"
                  checked={isRequired}
                  onCheckedChange={setIsRequired}
                />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Coding Assignment</CardTitle>
            </CardHeader>
            <CardContent>
              <Button
                className="w-full"
                onClick={() =>
                  router.push(
                    `${learningBase}/courses/${encodeURIComponent(courseId)}/assessments/${assessment.slug}/coding-definition`,
                  )
                }
              >
                <Code className="mr-2 h-4 w-4" />
                Edit Coding Definition
              </Button>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Actions</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              {error && <p className="text-destructive text-sm">{error}</p>}
              {saved && (
                <p className="text-sm text-green-600">Saved successfully.</p>
              )}

              <Button
                className="w-full"
                onClick={handleSave}
                disabled={isPending}
              >
                {isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Save className="mr-2 h-4 w-4" />
                )}
                Save Changes
              </Button>
              <Button variant="outline" className="w-full" onClick={handleBack}>
                Cancel
              </Button>

              <Separator />

              <Button
                variant="destructive"
                className="w-full"
                onClick={handleDelete}
                disabled={isDeleting || isLinkedQuiz}
              >
                {isDeleting ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Trash2 className="mr-2 h-4 w-4" />
                )}
                Delete Assessment
              </Button>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Info</CardTitle>
            </CardHeader>
            <CardContent className="text-muted-foreground space-y-1 text-sm">
              <p>Type: {typeLabel}</p>
              <p>Order: {assessment.order}</p>
              <p>
                Available:{" "}
                <Badge
                  variant={assessment.isAvailable ? "default" : "secondary"}
                  className="text-xs"
                >
                  {assessment.isAvailable ? "Yes" : "No"}
                </Badge>
              </p>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
