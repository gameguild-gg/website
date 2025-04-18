import { Api } from '@game-guild/apiclient';
import syllabusBody from './syllabus.md';
import { mockUser } from '@/data/coursesLib';
import chapter01 from './chapters/week01';
import chapter02 from './chapters/week02';
import chapter03 from './chapters/week03';
import chapter04 from './chapters/week04';
import chapter05 from './chapters/week05';
import chapter06 from './chapters/week06';
import chapter07 from './chapters/week07';
import chapter10 from './chapters/week10';
import chapter11 from './chapters/week11';
import chapter12 from './chapters/week12';
import ChapterEntity = Api.ChapterEntity;
import CourseEntity = Api.CourseEntity;
import ImageEntity = Api.ImageEntity;
import UserEntity = Api.UserEntity;
import LectureEntity = Api.LectureEntity;

export const mockImage: ImageEntity = {
  id: '1',
  createdAt: '2023-01-01T00:00:00Z',
  updatedAt: '2023-01-01T00:00:00Z',
  source: 'local',
  path: 'https://www.python.org/static/community_logos/',
  originalFilename: 'python-course.jpg',
  filename: 'python-logo.png',
  mimetype: 'image/jpeg',
  sizeBytes: 1024000,
  hash: 'abc123def456',
  width: 1200,
  height: 800,
  description: 'Python programming course thumbnail',
};

const course: CourseEntity = {
  title: 'Python Programming',
  slug: 'python',
  id: '3',
  summary:
    'Students will learn the history and basics of computing as well as the fundamentals of Python programming. General topics include: the history of computing, number systems, Boolean logic, algorithm design and implementation, and modern computer organization. Programming topics include: memory and variables, data types, mathematical operations, basic file I/O, decision-making, repetitions, functions, and list basics.',
  owner: mockUser as UserEntity,
  body: syllabusBody,
  chapters: [],
  lectures: [],
  thumbnail: mockImage,
  createdAt: '2023-01-01T00:00:00Z',
  updatedAt: '2023-01-01T00:00:00Z',
  visibility: Api.CourseEntity.Visibility.Enum.PUBLISHED,
  price: 0,
  subscriptionAccess: false,
};

const chapters: ChapterEntity[] = [];
chapters.push(chapter01);
chapters.push(chapter02);
chapters.push(chapter03);
chapters.push(chapter04);
chapters.push(chapter05);
chapters.push(chapter06);
chapters.push(chapter07);
chapters.push(chapter10);
chapters.push(chapter11);
chapters.push(chapter12);

const lectures: LectureEntity[] = [];
lectures.push(
  ...chapter01.lectures,
  ...chapter02.lectures,
  ...chapter03.lectures,
  ...chapter04.lectures,
  ...chapter05.lectures,
  ...chapter06.lectures,
  ...chapter07.lectures,
  ...chapter10.lectures,
  ...chapter11.lectures,
  ...chapter12.lectures,
);

// set the course for each chapter and lecture
for (let i = 0; i < chapters.length; i++) chapters[i].course = course;
for (let i = 0; i < lectures.length; i++) lectures[i].course = course;

course.chapters = chapters;
course.lectures = lectures;

export default course;
