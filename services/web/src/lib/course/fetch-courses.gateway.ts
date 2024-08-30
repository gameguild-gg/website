import {Course} from "@/lib/course/course";
import {COURSES} from "@/lib/course/courses.mock";
import {HttpClient} from "@/lib/core/http";

export type FetchCourses = {
  fetchCourses: () => Promise<ReadonlyArray<Course>>;
}

export class FetchCoursesGateway implements FetchCourses {
  constructor(readonly httpClient: HttpClient) {
  }

  public fetchCourses(): Promise<ReadonlyArray<Course>> {

    return Promise.resolve(COURSES);
  }
}