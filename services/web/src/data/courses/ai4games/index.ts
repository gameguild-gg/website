import { Api } from '@game-guild/apiclient';
import ChapterEntity = Api.ChapterEntity;
import CourseEntity = Api.CourseEntity;
import ImageEntity = Api.ImageEntity;
import UserEntity = Api.UserEntity;
import LectureEntity = Api.LectureEntity;
import syllabusBody from './syllabus.md';

const course: CourseEntity = {} as CourseEntity;

course.title = 'AI for Games';
course.slug = 'ai4games';
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

export default course;
