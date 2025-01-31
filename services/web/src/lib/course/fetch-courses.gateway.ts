import { COURSES } from '@/lib/course/courses.mock';
import { HttpClient } from '@/lib/core/http';
import { Api } from '@game-guild/apiclient';
import CourseEntity = Api.CourseEntity;

export type FetchCourses = {
  fetchCourses: () => Promise<ReadonlyArray<CourseEntity>>;
};

export class FetchCoursesGateway implements FetchCourses {
  constructor(readonly httpClient: HttpClient) {
  }

  public fetchCourses(): Promise<ReadonlyArray<CourseEntity>> {
    return Promise.resolve(COURSES);
  }
}
