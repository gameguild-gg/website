import { CourseEntity } from "src/cms/entities/course.entity";

export class CourseDto {

  price: number;
  subscriptionAccess: boolean;
  author: string;
  students: string[];
  lectures: string[];
  chapters: string[];

    constructor(course: CourseEntity) {
        // Object.assign(this, course);
        this.price = course.price;
        this.subscriptionAccess = course.subscriptionAccess;
        this.author = course.author.id;
        this.students = course.students.map((student) => student.id);
        this.lectures = course.lectures.map((lecture) => lecture.id);
        this.chapters = course.chapters.map((chapter) => chapter.id);
    }
}
