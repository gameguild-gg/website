import { Api } from '@game-guild/apiclient';
import ChapterEntity = Api.ChapterEntity;
import CourseEntity = Api.CourseEntity;
import ImageEntity = Api.ImageEntity;
import UserEntity = Api.UserEntity;
import LectureEntity = Api.LectureEntity;
import syllabusBody from './syllabus.md';
import { createChapter, createLecture } from '@/data/coursesLib';
import lecture01 from './chapters/week01/lecture.md';

const course: CourseEntity = {} as CourseEntity;

course.title = 'AI for Games';
course.slug = 'ai4games';
course.id = '1';
course.summary =
  'Students with a firm foundation in the basic techniques of artificial intelligence for games will apply their skills to program advanced pathfinding algorithms, artificial opponents, scripting tools and other real-time drivers for non-playable agents. The goal of the course is to provide finely-tuned artificial competition for players using all the rules followed by a human.';
course.owner = {
  id: '1',
  username: 'admin',
} as UserEntity;

course.body = syllabusBody;

const lectures: LectureEntity[] = [];
const chapters: ChapterEntity[] = [];

course.chapters = chapters;
course.lectures = lectures;
course.thumbnail = {
  filename: 'ai-in-gaming-main-1600-1.jpg',
  path: 'https://pixelplex.io/wp-content/uploads/2021/11',
  description: 'AI for Games course thumbnail',
} as ImageEntity;

const week01lectures: LectureEntity[] = [];
week01lectures.push(
  createLecture(
    '1-1',
    'randomness',
    'Random and Noise',
    'Random and Noise!',
    lecture01,
    1,
  ) as LectureEntity,
);

chapters.push(
  createChapter(
    '1',
    'week01',
    'Week 1: Noise and Randomness',
    'This week we will review basic concepts of AI for games and introduce the topics.',
    1,
    ['1-1'],
    week01lectures,
  ) as ChapterEntity,
);

for (let i = 0; i < week01lectures.length; i++) {
  week01lectures[i].chapter = chapters[0];
  week01lectures[i].course = course;
}

export default course;
