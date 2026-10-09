export function buildScenarioUrls(manifest) {
  const { webBaseUrl } = manifest.environment;
  const { assessmentId, contentId, courseSlug, submissionId } =
    manifest.resources;
  if (!courseSlug) return {};

  const locale = "en-US";
  const workspace = `${webBaseUrl}/${locale}/workspace/learning/courses/${encodeURIComponent(courseSlug)}`;
  const learnerHome = `${webBaseUrl}/${locale}/learn`;
  const learnerCourses = `${learnerHome}/courses`;
  const learner = `${learnerCourses}/${encodeURIComponent(courseSlug)}`;
  const urls = {
    course: workspace,
    contentList: `${workspace}/content`,
    assessmentList: `${workspace}/assessments`,
    learnerHome,
    learnerCourses,
    learnerCourse: learner,
    learnerActivities: `${learner}/activities`,
    learnerGrades: `${learner}/grades`,
  };
  if (contentId) {
    urls.contentEditor = `${workspace}/content/${encodeURIComponent(contentId)}`;
  }
  if (assessmentId) {
    urls.assessmentEditor = `${workspace}/assessments/${encodeURIComponent(assessmentId)}`;
    urls.testRun = urls.assessmentEditor;
    urls.learnerActivity = `${learner}/activities/assessment-${encodeURIComponent(assessmentId)}`;
    const query = new URLSearchParams({ course: courseSlug });
    if (submissionId) query.set("submission", submissionId);
    urls.speedGrader = `${webBaseUrl}/${locale}/speedgrader/assessments/${encodeURIComponent(assessmentId)}?${query}`;
  }
  return urls;
}
