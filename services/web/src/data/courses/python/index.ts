import { Api } from '@game-guild/apiclient';
import ChapterEntity = Api.ChapterEntity;
import CourseEntity = Api.CourseEntity;
import ImageEntity = Api.ImageEntity;
import UserEntity = Api.UserEntity;
import LectureEntity = Api.LectureEntity;
import syllabusBody from './syllabus.md';
import { createChapter, createLecture, mockUser } from '@/data/coursesLib';
import week01 from './chapters/week01/lecture.md';

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

const chapter: ChapterEntity = {
  id: '1',
  slug: 'week01',
  title: 'Week 01',
  summary: 'Introduction to Python',
  course,
  lectures: [],
} as ChapterEntity;

const week01lectures: LectureEntity[] = [];
week01lectures.push(
  createLecture(
    '1-1',
    'intro',
    'Course Introduction',
    'Welcome to the Python Programming course!',
    week01,
    1,
  ) as LectureEntity,
);

const chapters: ChapterEntity[] = [];

chapters.push(
  createChapter(
    '1',
    'week01',
    'Week 01: Introduction to Python',
    'This week we will introduce the course, ',
    1,
    ['1-1'],
    week01lectures,
  ) as ChapterEntity,
);

for (let i = 0; i < week01lectures.length; i++) {
  week01lectures[i].chapter = chapters[0];
  week01lectures[i].course = course;
}

chapters[0].course = course;

course.chapters = chapters;
course.lectures = week01lectures;

export default course;
