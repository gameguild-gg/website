'use server';

import {Course} from "@/lib/course/course";
import {httpClientFactory} from "@/lib/core/http";
import {FetchCoursesGateway} from "@/lib/course/fetch-courses.gateway";

export async function fetchCourses(): Promise<ReadonlyArray<Course>> {
  const gateway = new FetchCoursesGateway(httpClientFactory());

  return gateway.fetchCourses();
}