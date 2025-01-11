'use client';

import { useState, useEffect } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { ChevronRight, FileText, CheckCircle } from 'lucide-react';
import { ModuleBasev1_0_0 } from '@/lib/interface-base/module.base.v1.0.0';
import { AssessmentBasev1_0_0 } from '@/lib/interface-base/assessment.base.v1.0.0';
import { LessonBasev1_0_0 } from '@/lib/interface-base/lesson.base.v1.0.0';

export default function ModuleDetail({ params }: { params: { id: string } }) {
  const [module, setModule] = useState<ModuleBasev1_0_0 | null>(null);
  const [assessments, setAssessments] = useState<AssessmentBasev1_0_0[]>([]);
  const [lessons, setLessons] = useState<{ [key: number]: LessonBasev1_0_0 }>(
    {},
  );
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const router = useRouter();
  const searchParams = useSearchParams();
  const userId = searchParams.get('userId');
  const role = searchParams.get('role');
  const courseId = searchParams.get('courseId');
  const moduleId = searchParams.get('moduleId');

  useEffect(() => {
    const fetchModules = async () => {
      try {
        console.log(`Fetching module with id: ${params.id}`);
        const moduleResponse = await fetch(`/api/content/module/${params.id}`);
        if (!moduleResponse.ok) {
          const errorData = await moduleResponse.json();
          throw new Error(
            `Failed to fetch module: ${errorData.error}. Details: ${errorData.details}`,
          );
        }
        const moduleData: ModuleBasev1_0_0 = await moduleResponse.json();
        setModule(moduleData);

        const lessonPromises = moduleData.idLessons.map((id: number) =>
          fetch(`/api/content/lesson/${id}`).then((res) => res.json()),
        );
        const lessonData = await Promise.all(lessonPromises);
        const lessonMap = lessonData.reduce((acc, lesson) => {
          acc[lesson.id] = lesson;
          return acc;
        }, {});
        setLessons(lessonMap);

        const assessmentPromises = moduleData.idAssessments.map((id: number) =>
          fetch(`/api/content/assessment/${id}`).then(async (res) => {
            if (!res.ok) {
              const errorData = await res.json();
              throw new Error(
                `Failed to fetch assessment ${id}: ${errorData.error}. Details: ${errorData.details}`,
              );
            }
            return res.json();
          }),
        );
        const assessmentData = await Promise.all(assessmentPromises);
        setAssessments(assessmentData);
      } catch (error) {
        console.error('Error fetching module and assessments:', error);
        setError(
          error instanceof Error ? error.message : 'An unknown error occurred',
        );
      } finally {
        setIsLoading(false);
      }
    };
    fetchModules();
  }, [params.id]);

  if (isLoading) {
    return <div className="min-h-screen bg-gray-100 p-8">Loading...</div>;
  }

  if (error) {
    return (
      <div className="min-h-screen bg-gray-100 p-8">
        <h1 className="text-2xl font-bold mb-4 text-red-600">Error</h1>
        <p>{error}</p>
        <div className="mt-4">
          <Link href={`/course/${courseId}?userId=${userId}&role=${role}`}>
            <span className="text-blue-500 hover:underline">
              Back to Course
            </span>
          </Link>
        </div>
      </div>
    );
  }

  if (!module) {
    return (
      <div className="min-h-screen bg-gray-100 p-8">Module not found.</div>
    ); // More specific error message
  }

  return (
    <div className="space-y-4">
      {/* Lessons */}
      {module.idLessons.map((lessonId) => {
        const lesson = lessons[lessonId];
        return lesson ? (
          <div key={lesson.id} className="border rounded-md">
            <Link
              href={`/lesson/${lesson.id}?userId=${userId}&role=${role}&courseId=${courseId}&moduleId=${module.id}`}
            >
              <div className="w-full text-left px-4 py-2 flex items-center justify-between hover:bg-gray-50">
                <div className="flex items-center">
                  <FileText className="w-5 h-5 mr-2" />
                  <span>{lesson.title}</span>
                </div>
                <ChevronRight className="w-5 h-5" />
              </div>
            </Link>
          </div>
        ) : null;
      })}

      {/* Assessments */}
      {module.idAssessments.map((assessmentId) => {
        const assessment = assessments.find((a) => a.id === assessmentId);
        return assessment ? (
          <div key={assessment.id} className="border rounded-md">
            <Link
              href={`/assessment/${assessment.id}?userId=${userId}&role=${role}&courseId=${courseId}&moduleId=${module.id}`}
            >
              <div className="w-full text-left px-4 py-2 flex items-center justify-between hover:bg-gray-50">
                <div className="flex items-center">
                  <CheckCircle className="w-5 h-5 mr-2" />
                  <span>{assessment.title}</span>
                </div>
                <ChevronRight className="w-5 h-5" />
              </div>
            </Link>
          </div>
        ) : null;
      })}
    </div>
  );
}
