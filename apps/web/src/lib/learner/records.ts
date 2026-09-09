import { getToken } from "@/auth";
import {
  getLearnerDashboard,
  mapLearnerCourseSummary,
  type CourseAttendanceData,
} from "@/lib/learner/courses";
import {
  createServerClient,
  GeneratedApi,
  type LearningAssessmentsAssessment,
  type LearningAssessmentsLearnerAssessmentSubmission,
  type LearningCertificatesCertificate,
  type LearningCohortsCohort,
  type LearningCohortsCohortCalendarEntry,
  type LearningExperienceSocialServicesDiscussionReply,
  type LearningExperienceSocialServicesCourseDiscussion,
  type LearningWorkspacesLearnerAssessment,
  type LearningWorkspacesLearnerAssessmentSubmission,
  type LearningWorkspacesLearnerCertificate,
  type LearningWorkspacesLearnerCourseWorkspace,
  type LearningWorkspacesLearnerDiscussion,
  type LearningWorkspacesLearnerGradeSummary,
  type LearningWorkspacesLearnerScheduleEntry,
  type ProjectsProjectApiOutput,
} from "@game-guild/client";
import {
  optionalPercentUnitsToPercentage,
  optionalScoreUnitsToPoints,
  percentUnitsToPercentage,
  scoreUnitsToPoints,
} from "@/lib/learning/academic-values";

type LearnerCertificateRecord = LearningCertificatesCertificate & {
  verificationUrl?: string | null;
};

type AssessmentWithPassingScore = LearningAssessmentsAssessment & {
  passingScore?: number;
};

type LearnerAssessmentWithPassingScore =
  LearningWorkspacesLearnerAssessment & {
    passingScore?: number;
  };

export interface LearnerCourseRecord {
  course: CourseAttendanceData;
  context: LearnerCourseContext;
}
interface LearnerAssessmentGroupRecord {
  id: string;
  name: string;
  description?: string | null;
  weightPercent: number;
  order: number;
}

export interface LearnerCourseContext {
  enrollmentId: string | null;
  cohort: LearningCohortsCohort | null;
  calendar: LearningCohortsCohortCalendarEntry[];
  assessmentGroups: LearnerAssessmentGroupRecord[];
  assessments: LearningAssessmentsAssessment[];
  submissions: LearningAssessmentsLearnerAssessmentSubmission[];
  discussions: LearningExperienceSocialServicesCourseDiscussion[];
  certificates: LearnerCertificateRecord[];
  gradeSummary?: LearningWorkspacesLearnerGradeSummary;
}

function getApiUrl() {
  return (
    process.env.API_URL ||
    process.env.NEXT_PUBLIC_API_URL ||
    "http://localhost:8080"
  );
}

async function getClient() {
  const token = await getToken();
  if (!token) return null;
  return createServerClient({
    baseUrl: getApiUrl(),
    auth: { getAccessToken: async () => token },
  });
}

function emptyContext(): LearnerCourseContext {
  return {
    enrollmentId: null,
    cohort: null,
    calendar: [],
    assessmentGroups: [],
    assessments: [],
    submissions: [],
    discussions: [],
    certificates: [],
  };
}

function mapSchedule(
  entry: LearningWorkspacesLearnerScheduleEntry,
): LearningCohortsCohortCalendarEntry {
  return {
    cohortId: entry.cohortId,
    cohortName: entry.cohortName,
    itemId: entry.scheduleItemId,
    type: entry.type as LearningCohortsCohortCalendarEntry["type"],
    title: entry.title,
    startsAt: entry.startsAt,
    endsAt: entry.endsAt,
    availableFrom: entry.availableFrom,
    dueAt: entry.dueAt,
    status: entry.status as LearningCohortsCohortCalendarEntry["status"],
  };
}

function mapAssessment(
  assessment: LearningWorkspacesLearnerAssessment,
  courseId: string,
  groupNames: ReadonlyMap<string, string>,
): AssessmentWithPassingScore {
  const groupId = assessment.groupId ?? null;
  const assessmentWithPassingScore = assessment as LearnerAssessmentWithPassingScore;
  const passingScore = optionalScoreUnitsToPoints(
    assessmentWithPassingScore.passingScore,
  );

  return {
    id: assessment.assessmentId,
    courseId,
    contentId: assessment.contentId,
    title: assessment.title,
    description: assessment.description,
    type: (assessment.type === "Exam"
      ? "Quiz"
      : assessment.type) as LearningAssessmentsAssessment["type"],
    maxScore: scoreUnitsToPoints(assessment.maxScore ?? 0),
    passingScore: passingScore ?? undefined,
    timeLimitMinutes: assessment.timeLimitMinutes,
    maxAttempts: assessment.maxAttempts,
    isRequired: assessment.isRequired,
    order: assessment.order,
    availableFrom: assessment.availableFrom,
    availableUntil: assessment.availableUntil,
    assessmentGroupId: groupId,
    assessmentGroupName: groupId ? (groupNames.get(groupId) ?? null) : null,
    isAvailable: true,
    submissionModalities:
      assessment.submissionModalities as LearningAssessmentsAssessment["submissionModalities"],
    presentationMode:
      assessment.presentationMode as LearningAssessmentsAssessment["presentationMode"],
    dueAt: assessment.dueAt,
    allowLateSubmissions: assessment.allowLateSubmissions,
    lateSubmissionDeadline: assessment.lateSubmissionDeadline,
  };
}

function mapSubmission(
  submission: LearningWorkspacesLearnerAssessmentSubmission,
): LearningAssessmentsLearnerAssessmentSubmission {
  return {
    id: submission.submissionId,
    assessmentId: submission.assessmentId,
    enrollmentId: submission.enrollmentId,
    attemptNumber: submission.attemptNumber,
    score: optionalScoreUnitsToPoints(submission.score),
    passed: submission.passed,
    startedAt: submission.startedAt,
    submittedAt: submission.submittedAt,
    gradedAt: submission.gradedAt,
    feedback: submission.feedback,
    status:
      submission.status as LearningAssessmentsLearnerAssessmentSubmission["status"],
    isLate: submission.isLate,
  };
}

function mapDiscussion(
  discussion: LearningWorkspacesLearnerDiscussion,
  courseId: string,
): LearningExperienceSocialServicesCourseDiscussion {
  return {
    id: discussion.discussionId,
    courseId,
    contentId: discussion.contentId,
    authorId: discussion.authorId,
    title: discussion.title,
    content: discussion.content,
    isPinned: discussion.isPinned,
    isResolved: discussion.isResolved,
    replyCount: discussion.replyCount,
    viewCount: discussion.viewCount,
    lastActivityAt: discussion.lastActivityAt,
    createdAt: discussion.createdAt,
  };
}

function mapCertificate(
  certificate: LearningWorkspacesLearnerCertificate,
): LearnerCertificateRecord {
  return {
    id: certificate.certificateId,
    enrollmentId: certificate.enrollmentId,
    courseId: certificate.courseId,
    certificateNumber: certificate.certificateNumber,
    recipientName: certificate.recipientName,
    courseName: certificate.courseName,
    issuedAt: certificate.issuedAt,
    expiresAt: certificate.expiresAt,
    status: certificate.status as LearningCertificatesCertificate["status"],
    verificationUrl: certificate.verificationUrl,
  };
}

function mapWorkspaceContext(
  workspace: LearningWorkspacesLearnerCourseWorkspace,
  courseId: string,
): LearnerCourseContext {
  const groupNames = new Map(
    (workspace.assessmentGroups ?? [])
      .filter((group): group is typeof group & { groupId: string } =>
        Boolean(group.groupId),
      )
      .map((group) => [group.groupId, group.name ?? "Assessment group"]),
  );
  const cohort = workspace.cohort
    ? {
        id: workspace.cohort.cohortId,
        courseId,
        name: workspace.cohort.name,
        description: workspace.cohort.description,
        startDate: workspace.cohort.startDate,
        endDate: workspace.cohort.endDate,
        maxCapacity: workspace.cohort.maxCapacity,
        currentEnrollmentCount: workspace.cohort.currentEnrollmentCount,
        status: workspace.cohort.status as LearningCohortsCohort["status"],
        instructorId: workspace.cohort.instructorId,
        meetingSchedule: workspace.cohort.meetingSchedule,
      }
    : null;

  return {
    enrollmentId: workspace.course?.enrollmentId ?? null,
    cohort,
    calendar: (workspace.calendar ?? []).map(mapSchedule),
    assessmentGroups: (workspace.assessmentGroups ?? []).flatMap((group) =>
      group.groupId
        ? [
            {
              id: group.groupId,
              name: group.name ?? "Assessment group",
              description: group.description,
              weightPercent: percentUnitsToPercentage(group.weightPercent ?? 0),
              order: group.order ?? 0,
            },
          ]
        : [],
    ),
    assessments: (workspace.assessments ?? []).map((assessment) =>
      mapAssessment(assessment, courseId, groupNames),
    ),
    submissions: (workspace.submissions ?? []).map(mapSubmission),
    discussions: (workspace.discussions ?? []).map((discussion) =>
      mapDiscussion(discussion, courseId),
    ),
    certificates: (workspace.certificates ?? []).map(mapCertificate),
  };
}

export async function getCourseLearnerContext(
  courseId: string,
): Promise<LearnerCourseContext> {
  const client = await getClient();
  if (!client) return emptyContext();

  const result =
    await new GeneratedApi.LearningWorkspacesLearnerWorkspaceModule(
      client,
    ).getLearningCoursesWorkspace(courseId);
  return result.ok
    ? mapWorkspaceContext(result.data, courseId)
    : emptyContext();
}

export async function getMyCertificates(): Promise<
  LearningCertificatesCertificate[]
> {
  const client = await getClient();
  if (!client) return [];
  const result = await new GeneratedApi.LearningCertificatesModule(
    client,
  ).getApiCertificatesMy();
  return result.ok ? result.data : [];
}

export async function getMyProjects(
  userId: string,
): Promise<ProjectsProjectApiOutput[]> {
  const client = await getClient();
  if (!client || !userId) return [];
  const result = await new GeneratedApi.ProjectsModule(
    client,
  ).getProjectsCreator(userId, { take: 100 });
  return result.ok ? result.data : [];
}

export async function getMyLearnerRecords(): Promise<LearnerCourseRecord[]> {
  const dashboard = await getLearnerDashboard();
  if (!dashboard) return [];

  return (dashboard.courses ?? [])
    .map((summary) => {
      const course = mapLearnerCourseSummary(summary);
      const deadlines = (dashboard.deadlines ?? []).filter(
        (item) => item.courseId === course.id,
      );
      const gradeSummary = (dashboard.grades ?? []).find(
        (item) => item.courseId === course.id,
      );
      const gradeItems = gradeSummary?.items ?? [];
      const assessmentGroups = (gradeSummary?.groups ?? []).flatMap((group) =>
        group.groupId
          ? [
              {
                id: group.groupId,
                name: group.name ?? "Assessment group",
                description: group.description,
                weightPercent: percentUnitsToPercentage(group.weightPercent ?? 0),
                order: group.order ?? 0,
              },
            ]
          : [],
      );
      const groupNames = new Map(
        assessmentGroups.map((group) => [group.id, group.name]),
      );
      const assessmentSources = gradeItems.length > 0 ? gradeItems : deadlines;
      const context: LearnerCourseContext = {
        enrollmentId: course.enrollmentId ?? null,
        cohort: null,
        calendar: (dashboard.upcoming ?? [])
          .filter((entry) => entry.courseId === course.id)
          .map(mapSchedule),
        assessmentGroups,
        assessments: assessmentSources.flatMap((item) => {
          if (!item.assessmentId) return [];
          const groupId = item.groupId ?? null;
          return [
            {
              id: item.assessmentId,
              courseId: course.id,
              contentId: item.contentId,
              title: item.title,
              type: (item.type === "Exam"
                ? "Quiz"
                : item.type) as LearningAssessmentsAssessment["type"],
              maxScore: scoreUnitsToPoints(item.maxScore ?? 0),
              passingScore: (() => {
                const value = optionalScoreUnitsToPoints(
                  (item as typeof item & { passingScore?: number }).passingScore,
                );
                return value ?? undefined;
              })(),
              availableFrom: item.availableFrom,
              availableUntil: item.availableUntil,
              assessmentGroupId: groupId,
              assessmentGroupName: groupId
                ? (groupNames.get(groupId) ?? null)
                : null,
              dueAt: item.dueAt,
            },
          ];
        }),
        submissions: (gradeItems.length > 0 ? gradeItems : deadlines).flatMap(
          (item) => {
            if (
              !item.assessmentId ||
              !item.submissionStatus ||
              item.submissionStatus === "NotStarted"
            ) {
              return [];
            }

            return [
              {
                id: `dashboard-${item.assessmentId}`,
                assessmentId: item.assessmentId,
                enrollmentId: course.enrollmentId,
                status:
                  item.submissionStatus as LearningAssessmentsLearnerAssessmentSubmission["status"],
                ...("score" in item
                  ? {
                      score: optionalScoreUnitsToPoints(item.score),
                      passed: item.passed,
                      feedback: item.feedback,
                      gradedAt: item.gradedAt,
                    }
                  : {}),
              },
            ];
          },
        ),
        discussions: (dashboard.announcements ?? [])
          .filter((announcement) => announcement.courseId === course.id)
          .map((announcement) => ({
            id: announcement.discussionId,
            courseId: course.id,
            title: announcement.title,
            content: announcement.content,
            isPinned: true,
            lastActivityAt: announcement.lastActivityAt,
            createdAt: announcement.createdAt,
          })),
        certificates: (dashboard.certificates ?? [])
          .filter((certificate) => certificate.courseId === course.id)
          .map(mapCertificate),
        gradeSummary: gradeSummary
          ? {
              ...gradeSummary,
              finalGrade: optionalPercentUnitsToPercentage(gradeSummary.finalGrade),
              earnedPoints: optionalScoreUnitsToPoints(gradeSummary.earnedPoints),
              possiblePoints: optionalScoreUnitsToPoints(gradeSummary.possiblePoints),
              percentage: optionalPercentUnitsToPercentage(gradeSummary.percentage),
              groups: gradeSummary.groups?.map((group) => ({
                ...group,
                weightPercent: percentUnitsToPercentage(group.weightPercent ?? 0),
              })),
              items: gradeSummary.items?.map((item) => ({
                ...item,
                maxScore: scoreUnitsToPoints(item.maxScore ?? 0),
                score: optionalScoreUnitsToPoints(item.score),
              })),
            }
          : undefined,
      };

      return { course, context };
    })
    .filter((record) => Boolean(record.course.id && record.course.slug));
}

export interface LearnerDiscussionThread {
  discussion: LearningExperienceSocialServicesCourseDiscussion;
  replies: LearningExperienceSocialServicesDiscussionReply[];
}

export async function getCourseDiscussionThread(
  discussionId: string,
): Promise<LearnerDiscussionThread | null> {
  const client = await getClient();
  if (!client || !discussionId) return null;

  const discussions =
    new GeneratedApi.LearningExperienceSocialDiscussionsModule(client);
  const replies = new GeneratedApi.LearningExperienceSocialRepliesModule(
    client,
  );
  const [discussionResult, repliesResult] = await Promise.all([
    discussions.getApiSocialDiscussions(discussionId),
    replies.getApiSocialDiscussionsReplies(discussionId, { take: 200 }),
  ]);

  if (!discussionResult.ok || !repliesResult.ok) {
    return null;
  }

  return {
    discussion: discussionResult.data,
    replies: repliesResult.data,
  };
}
