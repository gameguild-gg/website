'use server';

import { httpClientFactory } from '@/lib/core/http';
import { FetchCoursesGateway } from '@/lib/course/fetch-courses.gateway';
import { Api } from '@game-guild/apiclient';
import CourseEntity = Api.CourseEntity;

export async function fetchCourses(): Promise<ReadonlyArray<CourseEntity>> {
  const gateway = new FetchCoursesGateway(httpClientFactory());

  return gateway.fetchCourses();
}
