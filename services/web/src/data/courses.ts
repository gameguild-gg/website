import { Api } from '@game-guild/apiclient';
import UserEntity = Api.UserEntity;
import ImageEntity = Api.ImageEntity;
import CourseEntity = Api.CourseEntity;
import ChapterEntity = Api.ChapterEntity;
import LectureEntity = Api.LectureEntity;
import portfolio from '@/data/courses/portfolio';
import ai4games from '@/data/courses/ai4games';
import python from '@/data/courses/python';

// Create the course
const courses: CourseEntity[] = [ai4games, portfolio, python];

// Associate the course to chapters and lectures
courses.forEach((course) => {
  course.chapters.forEach((chapter) => {
    (chapter as ChapterEntity).course = course;
    chapter.lectures?.forEach((lecture) => {
      (lecture as LectureEntity).course = course;
      (lecture as LectureEntity).chapter = chapter as ChapterEntity;
    });
  });
});

// Flatten lectures for easier access if needed
const allLectures = courses.flatMap((course) =>
  course.chapters.flatMap((chapter) => chapter.lectures),
);

export { courses, allLectures };
