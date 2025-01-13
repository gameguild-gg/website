import ai4games from '@/data/courses/ai4games';
import { Api } from '@game-guild/apiclient';
import UserEntity = Api.UserEntity;
import ImageEntity = Api.ImageEntity;
import CourseEntity = Api.CourseEntity;
import ChapterEntity = Api.ChapterEntity;
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

// Create lectures (without course and chapter references initially)
const createLecture = (
  id: string,
  slug: string,
  title: string,
  summary: string,
  body: string,
  order: number,
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
});

const lecturesData = [
  createLecture(
    '1-1',
    'intro-to-python',
    'Introduction to Python',
    'An overview of Python and its applications',
    `
# Welcome to Python Programming!

In this lecture, we'll cover the basics of Python and why it's a great language for beginners.

## Why Python?

- Easy to learn and read
- Versatile and powerful
- Large community and extensive libraries

::: note "Python's Versatility"
Python is widely used in various fields, including web development, data science, artificial intelligence, and more!
:::

## What You'll Learn

1. Basic syntax
2. Data types
3. Control structures
4. Functions

Let's visualize our learning path:

\`\`\`mermaid
graph TD
    A[Start] --> B[Python Basics]
    B --> C[Control Structures]
    C --> D[Functions]
    D --> E[Advanced Topics]
\`\`\`

Let's get started with a simple "Hello, World!" program:

\`\`\`python
print("Hello, World!")
\`\`\`

::: tip "Practice Makes Perfect"
Remember to practice regularly and experiment with different code examples to reinforce your learning!
:::
  `,
    1,
  ),
  createLecture(
    '1-2',
    'setup-environment',
    'Setting Up Your Environment',
    'Installing Python and setting up your development environment',
    "# Setting Up Your Python Environment\n\n## Installing Python\n\n1. Visit [python.org](https://www.python.org)\n2. Download the latest version for your OS\n3. Run the installer and follow the prompts\n\n## Verifying Installation\n\nOpen a terminal and type:\n\n```\npython --version\n```\n\nYou should see the installed Python version.\n\n## Creating Your First Python Script\n\n1. Open a text editor\n2. Type the following:\n\n```python\nprint('Hello, World!')\n```\n\n3. Save the file as `hello.py`\n4. Run it from the terminal:\n\n```\npython hello.py\n```\n\nCongratulations! You've run your first Python script.",
    2,
  ),
  createLecture(
    '1-3',
    'basic-syntax',
    'Basic Syntax',
    "Understanding Python's basic syntax and structure",
    "# Python Basic Syntax\n\n## Variables and Data Types\n\n```python\n# Strings\nname = 'John Doe'\n\n# Integers\nage = 30\n\n# Floats\nheight = 1.75\n\n# Booleans\nis_student = True\n```\n\n## Basic Operations\n\n```python\n# Addition\nresult = 5 + 3\nprint(result)  # Output: 8\n\n# String concatenation\nfull_name = 'John' + ' ' + 'Doe'\nprint(full_name)  # Output: John Doe\n```\n\n## Control Structures\n\n### If-Else Statement\n\n```python\nage = 20\nif age >= 18:\n    print('You are an adult')\nelse:\n    print('You are a minor')\n```\n\n### For Loop\n\n```python\nfor i in range(5):\n    print(i)\n```\n\nThis covers the basic syntax of Python. Practice these concepts to build a strong foundation!",
    3,
  ),
];

// Create chapters (without course reference initially)
const createChapter = (
  id: string,
  slug: string,
  title: string,
  summary: string,
  order: number,
  lectureIds: string[],
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

const chaptersData = [
  createChapter(
    '1',
    'getting-started-with-python',
    'Getting Started with Python',
    'Introduction to Python and setting up your environment',
    1,
    ['1-1', '1-2', '1-3'],
  ),
];

// Create the course
const courses: CourseEntity[] = [
  ai4games,
  {
    id: '1',
    createdAt: '2023-01-01T00:00:00Z',
    updatedAt: '2023-01-01T00:00:00Z',
    owner: mockUser,
    slug: 'introduction-to-programming-through-python',
    title: 'Introduction to Programming through Python',
    summary:
      'Learn the basics of programming using Python, a versatile and beginner-friendly language.',
    body: `
# Introduction to Programming through Python

Welcome to this comprehensive course on programming fundamentals using Python!

## Course Overview

This course will guide you through the fundamentals of programming using Python. You'll start with the basics and progress to more advanced concepts, all while building practical skills that you can apply to real-world problems.

::: important "For Beginners"
This course is designed for beginners. No prior programming experience is required!
:::

## What You'll Learn

1. **Python Basics**: Syntax, variables, and data types
2. **Control Structures**: If-else statements, loops, and functions
3. **Data Structures**: Lists, tuples, dictionaries, and sets
4. **Object-Oriented Programming**: Classes and objects
5. **File Handling**: Reading and writing files
6. **Error Handling**: Try-except blocks and debugging
7. **Modules and Packages**: Using built-in and third-party libraries

## Course Structure

\`\`\`mermaid
graph TD
    A[Python Basics] --> B[Control Structures]
    B --> C[Data Structures]
    C --> D[OOP]
    D --> E[File Handling]
    E --> F[Error Handling]
    F --> G[Modules and Packages]
\`\`\`

## Why Python?

Python is an excellent language for beginners due to its:

- Clear and readable syntax
- Versatility across various domains (web development, data science, AI, etc.)
- Large and supportive community
- Extensive library ecosystem

::: note "Industry Relevance"
Python's popularity in the industry means that the skills you learn in this course will be highly valuable in the job market!
:::

## Sample Code

Here's a taste of what you'll be writing by the end of this course:

\`\`\`python
class BankAccount:
    def __init__(self, owner, balance=0):
        self.owner = owner
        self.balance = balance
    
    def deposit(self, amount):
        self.balance += amount
        print(f"Deposited \${amount}. New balance: \${self.balance}")
    
    def withdraw(self, amount):
        if amount <= self.balance:
            self.balance -= amount
            print(f"Withdrew \${amount}. New balance: \${self.balance}")
        else:
            print("Insufficient funds!")

# Usage
account = BankAccount("John Doe", 1000)
account.deposit(500)
account.withdraw(200)
account.withdraw(2000)
\`\`\`

::: tip "Keep Practicing"
Remember, the key to mastering programming is practice. Don't be afraid to experiment and make mistakes!
:::

Let's embark on this exciting journey into the world of programming with Python!
    `,
    visibility: Api.CourseEntity.Visibility.Enum.PUBLISHED,
    thumbnail: mockImage,
    price: 0,
    subscriptionAccess: false,
    chapters: chaptersData as ChapterEntity[],
  } as CourseEntity,
];

// Associate the course to chapters and lectures
courses.forEach((course) => {
  course.chapters.forEach((chapter) => {
    (chapter as ChapterEntity).course = course;
    chapter.lectures.forEach((lecture) => {
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
