import { Api } from '@game-guild/apiclient';
import ChapterEntity = Api.ChapterEntity;
import CourseEntity = Api.CourseEntity;
import ImageEntity = Api.ImageEntity;
import UserEntity = Api.UserEntity;
import LectureEntity = Api.LectureEntity;
import syllabusBody from './syllabus.md';
import { mockImage } from '@/data/coursesLib';

import chapter01 from './chapters/week01/index';

const course: CourseEntity = {} as CourseEntity;
course.id = '2';
course.title = `Game Developer's Portfolio`;
course.slug = 'portfolio';
course.summary =
  "Creating and maintaining a portfolio is a crucial part in any game developer's job search and career.? Portfolios are especially challenging for programmers, since the work presented is not inherently visual, yet it must still effectively demonstrate the individual's prowess and skills in their discipline.? This course provides Game Programmers a formal opportunity to sum up their experience in the major and produce a portfolio worthy of presentation at the Senior Show.? In this course, students discuss and implement pertinent portfolio materials for programmers, such as websites, repositories and demo reels.? Students will have an opportunity to spearhead an entirely solo project to add as a centerpiece to their materials.";
course.owner = {
  id: '1',
  username: 'admin',
} as UserEntity;
course.visibility = Api.CourseEntity.Visibility.Enum.PUBLISHED;
course.thumbnail = mockImage;
course.price = 0;
course.subscriptionAccess = false;

course.body = syllabusBody;

const chapters: ChapterEntity[] = [];
chapters.push(chapter01);

const lectures: LectureEntity[] = [];
lectures.push(...chapter01.lectures);

// set course for all lectures and chapters
lectures.forEach((lecture) => (lecture.course = course));
chapters.forEach((chapter) => (chapter.course = course));
course.chapters = chapters;
course.lectures = lectures;

course.thumbnail = {
  filename: 'sddefault.jpg',
  path: 'https://i.ytimg.com/vi/gh_uqsNakZc',
  description: 'Game developers portfolio banner',
} as ImageEntity;

export default course;
