export type CourseIdentifier = string;

export type Course = {
  id: CourseIdentifier;
  slug: string;
  name: string;
  description: string;
  thumbnailUrl: string;
}
