import { Api } from '@game-guild/apiclient';
import ChapterEntity = Api.ChapterEntity;
import CourseEntity = Api.CourseEntity;
import ImageEntity = Api.ImageEntity;
import UserEntity = Api.UserEntity;
import LectureEntity = Api.LectureEntity;

// Mock user data
const mockUser: UserEntity = {
  id: '1',
  createdAt: '2023-01-01T00:00:00Z',
  updatedAt: '2023-01-01T00:00:00Z',
  username: 'johndoe',
  email: 'john@example.com',
} as UserEntity;

// Mock image data
const mockImage: ImageEntity = {
  id: '1',
  createdAt: '2023-01-01T00:00:00Z',
  updatedAt: '2023-01-01T00:00:00Z',
  source: 'local',
  path: '/images/courses',
  originalFilename: 'python-course.jpg',
  filename: 'python-course-1234567890.jpg',
  mimetype: 'image/jpeg',
  sizeBytes: 1024000,
  hash: 'abc123def456',
  width: 1200,
  height: 800,
  description: 'Python programming course thumbnail',
};

// Create lectures
const lectures: LectureEntity[] = [
  {
    id: '1-1',
    createdAt: '2023-01-01T00:00:00Z',
    updatedAt: '2023-01-01T00:00:00Z',
    owner: mockUser,
    slug: 'intro-to-python',
    title: 'Introduction to Python',
    summary: 'An overview of Python and its applications',
    body: "<p>Welcome to Python programming! In this lecture, we'll cover the basics of Python and why it's a great language for beginners.</p>",
    visibility: Api.CourseEntity.Visibility.Enum.PUBLISHED,
    order: 1,
    course: {} as CourseEntity, // This will be filled in later
    chapter: {} as ChapterEntity, // This will be filled in later
  },
  {
    id: '1-2',
    createdAt: '2023-01-01T00:00:00Z',
    updatedAt: '2023-01-01T00:00:00Z',
    owner: mockUser,
    slug: 'setup-environment',
    title: 'Setting Up Your Environment',
    summary: 'Installing Python and setting up your development environment',
    body: '<p>Learn how to set up Python on your computer and create your first Python script.</p>',
    visibility: Api.CourseEntity.Visibility.Enum.PUBLISHED,
    order: 2,
    course: {} as CourseEntity,
    chapter: {} as ChapterEntity,
  },
  {
    id: '1-3',
    createdAt: '2023-01-01T00:00:00Z',
    updatedAt: '2023-01-01T00:00:00Z',
    owner: mockUser,
    slug: 'basic-syntax',
    title: 'Basic Syntax',
    summary: "Understanding Python's basic syntax and structure",
    body: '<p>Explore the fundamental syntax of Python, including variables, data types, and basic operations.</p>',
    visibility: Api.CourseEntity.Visibility.Enum.PUBLISHED,
    order: 3,
    course: {} as CourseEntity,
    chapter: {} as ChapterEntity,
  },
  {
    id: '2-1',
    createdAt: '2023-01-02T00:00:00Z',
    updatedAt: '2023-01-02T00:00:00Z',
    owner: mockUser,
    slug: 'numbers-and-strings',
    title: 'Numbers and Strings',
    summary: 'Working with numeric and string data types in Python',
    body: '<p>Dive deeper into numeric and string data types in Python.</p>',
    visibility: Api.CourseEntity.Visibility.Enum.PUBLISHED,
    order: 1,
    course: {} as CourseEntity,
    chapter: {} as ChapterEntity,
  },
  {
    id: '2-2',
    createdAt: '2023-01-02T00:00:00Z',
    updatedAt: '2023-01-02T00:00:00Z',
    owner: mockUser,
    slug: 'lists-and-tuples',
    title: 'Lists and Tuples',
    summary: 'Understanding and using sequence types in Python',
    body: '<p>Learn about sequence types in Python: lists and tuples.</p>',
    visibility: Api.CourseEntity.Visibility.Enum.PUBLISHED,
    order: 2,
    course: {} as CourseEntity,
    chapter: {} as ChapterEntity,
  },
  {
    id: '2-3',
    createdAt: '2023-01-02T00:00:00Z',
    updatedAt: '2023-01-02T00:00:00Z',
    owner: mockUser,
    slug: 'dictionaries',
    title: 'Dictionaries',
    summary: 'Working with key-value pairs using Python dictionaries',
    body: '<p>Explore the powerful dictionary data structure in Python.</p>',
    visibility: Api.CourseEntity.Visibility.Enum.PUBLISHED,
    order: 3,
    course: {} as CourseEntity,
    chapter: {} as ChapterEntity,
  },
];

// Create chapters and associate lectures
const chapters: ChapterEntity[] = [
  {
    id: '1',
    createdAt: '2023-01-01T00:00:00Z',
    updatedAt: '2023-01-01T00:00:00Z',
    owner: mockUser,
    slug: 'getting-started-with-python',
    title: 'Getting Started with Python',
    summary: 'Introduction to Python and setting up your environment',
    visibility: Api.CourseEntity.Visibility.Enum.PUBLISHED,
    order: 1,
    course: {} as CourseEntity, // This will be filled in later
    lectures: [lectures[0], lectures[1], lectures[2]],
  },
  {
    id: '2',
    createdAt: '2023-01-02T00:00:00Z',
    updatedAt: '2023-01-02T00:00:00Z',
    owner: mockUser,
    slug: 'data-types-and-variables',
    title: 'Data Types and Variables',
    summary: "Exploring Python's basic data types and how to work with them",
    visibility: Api.CourseEntity.Visibility.Enum.PUBLISHED,
    order: 2,
    course: {} as CourseEntity,
    lectures: [lectures[3], lectures[4], lectures[5]],
  },
];

// Associate chapters to lectures
chapters.forEach((chapter) => {
  chapter.lectures.forEach((lecture) => {
    lecture.chapter = chapter;
  });
});

// Create the course and associate chapters and lectures
export const courses: CourseEntity[] = [
  {
    id: '1',
    createdAt: '2023-01-01T00:00:00Z',
    updatedAt: '2023-01-01T00:00:00Z',
    owner: mockUser,
    slug: 'introduction-to-programming-through-python',
    title: 'Introduction to Programming through Python',
    summary:
      'Learn the basics of programming using Python, a versatile and beginner-friendly language.',
    body: "This course will guide you through the fundamentals of programming using Python. You'll start with the basics and progress to more advanced concepts, all while building practical skills that you can apply to real-world problems.",
    visibility: Api.CourseEntity.Visibility.Enum.PUBLISHED,
    thumbnail: mockImage,
    price: 0,
    subscriptionAccess: false,
    chapters: chapters,
  } as CourseEntity,
];

// Associate the course to chapters and lectures
courses.forEach((course) => {
  course.chapters.forEach((chapter) => {
    chapter.course = course;
    chapter.lectures.forEach((lecture) => {
      lecture.course = course;
    });
  });
});

// Flatten lectures for easier access if needed
export const allLectures = courses.flatMap((course) =>
  course.chapters.flatMap((chapter) => chapter.lectures),
);
