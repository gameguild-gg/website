// Create lectures (without course and chapter references initially)
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ImageEntity = Api.ImageEntity;
import UserEntity = Api.UserEntity;
import ChapterEntity = Api.ChapterEntity;
import CourseEntity = Api.CourseEntity;

// Mock user data
export const mockUser: UserEntity = {
  id: '1',
  createdAt: '2023-01-01T00:00:00Z',
  updatedAt: '2023-01-01T00:00:00Z',
  username: 'johndoe',
  email: 'john@example.com',
} as UserEntity;

// Mock image data
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

export const createLecture = (
  id: string,
  slug: string,
  title: string,
  summary: string,
  body: string,
  order: number,
  renderer: LectureEntity.Renderer = LectureEntity.Renderer.Enum.Markdown,
): Omit<LectureEntity, 'course' | 'chapter'> => ({
  id,
  createdAt: '2023-01-01T00:00:00Z',
  updatedAt: '2023-01-01T00:00:00Z',
  owner: mockUser,
  slug,
  title,
  summary,
  body,
  visibility: Api.CourseEntity.Visibility.Enum.PUBLISHED,
  order,
  renderer,
});

// Create chapters (without course reference initially)
export const createChapter = (
  id: string,
  slug: string,
  title: string,
  summary: string,
  order: number,
  lectureIds: string[],
  lecturesData: LectureEntity[],
): Omit<ChapterEntity, 'course'> => ({
  id,
  createdAt: '2023-01-01T00:00:00Z',
  updatedAt: '2023-01-01T00:00:00Z',
  owner: mockUser,
  slug,
  title,
  summary,
  visibility: Api.CourseEntity.Visibility.Enum.PUBLISHED,
  order,
  lectures: lectureIds.map((id) => ({
    ...lecturesData.find((l) => l.id === id)!,
    course: {} as CourseEntity,
    chapter: {} as ChapterEntity,
  })),
});
