export function buildScenarioUrls(manifest) {
  const { webBaseUrl } = manifest.environment;
  const { courseSlug } = manifest.resources;
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
    learnerReviews: `${learnerHome}/reviews`,
    quizzes: {},
  };

  for (const quizKey of manifest.quizKeys ?? []) {
    const resources = manifest.resources.quizzes?.[quizKey];
    if (!resources) continue;
    const quizUrls = {};
    if (resources.contentId) {
      quizUrls.contentEditor = `${workspace}/content/${encodeURIComponent(resources.contentId)}`;
    }
    if (resources.assessmentId) {
      quizUrls.assessmentEditor = `${workspace}/assessments/${encodeURIComponent(resources.assessmentId)}`;
      quizUrls.testRun = quizUrls.assessmentEditor;
      quizUrls.learnerActivity = `${learner}/activities/assessment-${encodeURIComponent(resources.assessmentId)}`;
      quizUrls.speedGraders = {};
      for (const [personaKey, submission] of Object.entries(
        resources.submissions ?? {},
      )) {
        const query = new URLSearchParams({ course: courseSlug });
        if (submission.submissionId) {
          query.set("submission", submission.submissionId);
        }
        quizUrls.speedGraders[personaKey] =
          `${webBaseUrl}/${locale}/speedgrader/assessments/${encodeURIComponent(resources.assessmentId)}?${query}`;
      }
      quizUrls.speedGrader =
        quizUrls.speedGraders.learnerA ??
        `${webBaseUrl}/${locale}/speedgrader/assessments/${encodeURIComponent(resources.assessmentId)}?course=${encodeURIComponent(courseSlug)}`;
    }
    urls.quizzes[quizKey] = quizUrls;
  }

  if (manifest.quizKeys?.length === 1) {
    Object.assign(urls, urls.quizzes[manifest.quizKeys[0]]);
  }
  return urls;
}
