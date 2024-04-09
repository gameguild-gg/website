import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { CourseEntity } from './entities/course.entity';
import { LectureEntity } from './entities/lecture.entity';
import { ChapterEntity } from './entities/chapter.entity';
import { PostEntity } from './entities/post.entity';
import { UserEntity } from '../user/entities';
import { CreateCourseDto } from '../dtos/course/create-course.dto';
import { UserService } from '../user/user.service';
import { UpdateCourseDto } from 'src/dtos/course/update-course.dto';
import { FindCourseDto } from 'src/dtos/course/find-course.dto';
import { UpdateChapterDto } from 'src/dtos/chapter/update-chapter.dto';
import { CreateChapterDto } from 'src/dtos/chapter/create-chapter.dto';
import { UpdateLectureDto } from 'src/dtos/lecture/update-lecture.dto';
import { CreateLectureDto } from 'src/dtos/lecture/create-lecture.dto';

@Injectable()
export class ContentService {
  private readonly logger = new Logger(ContentService.name);

  constructor(
    @InjectRepository(CourseEntity)
    private readonly courseRepository: Repository<CourseEntity>,
    @InjectRepository(LectureEntity)
    private readonly lectureRepository: Repository<LectureEntity>,
    @InjectRepository(ChapterEntity)
    private readonly chapterRepository: Repository<ChapterEntity>,
    @InjectRepository(PostEntity)
    private readonly postRepository: Repository<PostEntity>,
    private readonly userService: UserService,
  ) {}

  //#region Courses
  // TODO: refactor the other functions to return a CourseDto and this one only to return the CourseEntity
  async findCourseBy(course: FindCourseDto): Promise<CourseEntity> {
    return null;
  }

  async findAllCoursesBy(course: FindCourseDto): Promise<CourseEntity> {
    return null;
  }
    
  async createEmptyCourse(user: UserEntity): Promise<CourseEntity> {
    let course = new CourseEntity();
    user = await this.userService.findOne({ where: { id: user.id } });
    course.author = user;
    course = await this.courseRepository.save(course);
    return course;
    // return this.courseRepository.findOne({
    //   where: { id: course.id },
    // });
  }

  async createCourse(user: UserEntity, createCourse: CreateCourseDto): Promise<CourseEntity> {
    // check if course is created with empty lectures
    if (createCourse.chapters.length === 0) {
      return await this.createEmptyCourse(user);
    }

    let course = new CourseEntity();
    
    // get author info
    user = await this.userService.findOne({ where: { id: user.id } });
    course.author = user;

    // create lectures
    let lectures = [];
    // createCourse.lectures.forEach(async (lecture) => {
    //   lectures.push(await this.createEmptyLecture()); 
    // });
    
    // create chapters
    let chapters = [];
    // createCourse.chapters.forEach(async (chapter) => {
    //   chapters.push(await this.createEmptyChapter()); 
    // });

    // fill course data
    course = { 
      ...course,
      ...createCourse,
      author: user,
      lectures,
      chapters,
    };

    // store course
    return await this.courseRepository.save(course);;
  }

  async getCourse(user: UserEntity, courseId: string): Promise<CourseEntity> {
    // TODO: check if user has permission to this course
    return await this.courseRepository.findOneBy({ id: courseId });
  }

  async getAllCourses(): Promise<CourseEntity[]> {
    return await this.courseRepository.find();
  }

  async updateCourse(user: UserEntity, update: UpdateCourseDto): Promise<CourseEntity> {
    let updateCourse = await this.getCourse(user, update.id);
    // TODO: check if user has permission to update the course
    updateCourse = { ...updateCourse, ...update };
    await this.courseRepository.update(update.id, updateCourse);
    return await this.getCourse(user, update.id); 
  }

  async deleteCourse(user: UserEntity, id: string): Promise<CourseEntity> {
    const deletedCourse = await this.getCourse(user, id);
    // TODO: check if user has permission to delete the course
    await this.courseRepository.delete(deletedCourse);
    return deletedCourse;
  }
  //#endregion

  //#region Chapters
  async createEmptyChapter(user: UserEntity): Promise<ChapterEntity> {
    return null;
  }

  async createChapter(user: UserEntity, chapter: CreateChapterDto): Promise<ChapterEntity> {
    const course = await this.getCourse(user, chapter.courseId);
    // TODO: check if user is the author of the course
    let newChapter = new ChapterEntity();
    newChapter = { ...newChapter, ...chapter };
    return await this.chapterRepository.save(newChapter);
  }

  async getChapter(user: UserEntity, chapterId: string): Promise<ChapterEntity> {
    return await this.chapterRepository.findOneBy({ id: chapterId });
  }

  async getAllChapters(user: UserEntity, courseId: string): Promise<ChapterEntity[]> {
    const course = await this.getCourse(user, courseId);
    return course.chapters;
  }

  async updateChapter(user: UserEntity, update: UpdateChapterDto): Promise<ChapterEntity> {
    let updateCourse = await this.getChapter(user, update.id);
    updateCourse = { ...updateCourse, ...update };
    await this.courseRepository.update(update.id, updateCourse);
    return await this.getChapter(user, update.id);
  }

  async deleteChapter(user: UserEntity, id: string): Promise<ChapterEntity> {
    const deleted = await this.getChapter(user, id);
    await this.chapterRepository.delete(deleted);
    return deleted;
  }
  //#endregion

  //#region Lectures
  async createEmptyLecture(user: UserEntity): Promise<LectureEntity> { 
    return null;
  }

  async createLecture(user: UserEntity, lecture: CreateLectureDto): Promise<LectureEntity> { 
    let newLecture = new LectureEntity();
    newLecture = { 
      ...newLecture,
      ...lecture
    };
    
    return null;
  }

  async getLecture(id: string): Promise<LectureEntity> {
    return await this.lectureRepository.findOneBy({ id });
  }

  async getAllLectures(user: UserEntity, chapterId: string): Promise<LectureEntity[]> {
    const chapter = await this.getChapter(user, chapterId);
    return chapter.lectures;
  }

  async updateLecture(update: UpdateLectureDto): Promise<LectureEntity> {
    let updateCourse = await this.getLecture(update.id);
    updateCourse = { ...updateCourse, ...update };
    await this.courseRepository.update(update.id, updateCourse);
    return await this.getLecture(update.id);
  }

  async deleteLecture(id: string): Promise<LectureEntity> { 
    const deleted = await this.getLecture(id);
    await this.chapterRepository.delete(deleted);
    return deleted;
  }

  //#endregion
}
