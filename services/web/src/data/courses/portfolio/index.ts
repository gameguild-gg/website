import { Api } from '@game-guild/apiclient';
import ChapterEntity = Api.ChapterEntity;
import CourseEntity = Api.CourseEntity;
import ImageEntity = Api.ImageEntity;
import UserEntity = Api.UserEntity;
import LectureEntity = Api.LectureEntity;
import syllabusBody from './syllabus.md';
import lecture01 from './chapters/week01/lecture.md';
import assignment01 from './chapters/week01/assignment01.md';
import assignment02 from './chapters/week01/assignment02.md';
import { createLecture } from '@/data/coursesLib';

const course: CourseEntity = {} as CourseEntity;

course.title = `Game Developer's Portfolio`;
course.slug = 'portfolio';
course.summary =
  "Creating and maintaining a portfolio is a crucial part in any game developer's job search and career.? Portfolios are especially challenging for programmers, since the work presented is not inherently visual, yet it must still effectively demonstrate the individual's prowess and skills in their discipline.? This course provides Game Programmers a formal opportunity to sum up their experience in the major and produce a portfolio worthy of presentation at the Senior Show.? In this course, students discuss and implement pertinent portfolio materials for programmers, such as websites, repositories and demo reels.? Students will have an opportunity to spearhead an entirely solo project to add as a centerpiece to their materials.";
course.owner = {
  id: '1',
  username: 'admin',
} as UserEntity;

course.body = syllabusBody;

const week01lectures: LectureEntity[] = [];
week01lectures.push(
  createLecture(
    '1-1',
    'portfolio',
    'Introduction to Portfolio',
    'Welcome to the Game Developer Portfolio course!',
    lecture01,
    1,
  ) as LectureEntity,
);
week01lectures.push(
  createLecture(
    '1-2',
    'assignment-01',
    'Assignment 01',
    'Solo Project Proposal',
    assignment01,
    2,
  ) as LectureEntity,
);
week01lectures.push(
  createLecture(
    '1-3',
    'assignment-02',
    'Assignment 02',
    'Portfolio Analysis',
    assignment02,
    3,
  ) as LectureEntity,
);

const chapters: ChapterEntity[] = [];

chapters.push(
  createLecture('1', 'Week 1', 'Week 1', 'Week 1', '', 1) as ChapterEntity,
);

for (let i = 0; i < week01lectures.length; i++) {
  week01lectures[i].chapter = chapters[0];
  week01lectures[i].course = course;
}

chapters[0].course = course;

course.chapters = chapters;
course.lectures = week01lectures;
course.thumbnail = {
  filename: 'sddefault.jpg',
  path: 'https://i.ytimg.com/vi/gh_uqsNakZc',
  description: 'Game developers portfolio banner',
} as ImageEntity;

export default course;
