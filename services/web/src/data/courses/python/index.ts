import { Api } from '@game-guild/apiclient';
import ChapterEntity = Api.ChapterEntity;
import CourseEntity = Api.CourseEntity;
import ImageEntity = Api.ImageEntity;
import UserEntity = Api.UserEntity;
import syllabusBody from './syllabus.md';
import { mockUser } from '@/data/coursesLib';
import chapter01 from './chapters/week01';
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

const lectures: LectureEntity[] = [];
lectures.push(...chapter01.lectures);

// set the course for each chapter and lecture
for (let i = 0; i < chapters.length; i++) chapters[i].course = course;
for (let i = 0; i < lectures.length; i++) lectures[i].course = course;

course.chapters = chapters;
course.lectures = lectures;

export default course;
