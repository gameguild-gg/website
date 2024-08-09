import { Injectable } from '@nestjs/common';
import { CreateCourseDto } from './dto/create-course.dto';
import { UpdateCourseDto } from './dto/update-course.dto';
import { CourseDto } from './dto/course.dto';
import { InjectRepository } from '@nestjs/typeorm';
import { CourseEntity } from '../entities/course.entity';
import { Repository } from 'typeorm';
import { UserService } from '../../user/user.service';

@Injectable()
export class CourseService {

  constructor(
    @InjectRepository(CourseEntity)
    private readonly repo: Repository<CourseEntity>,
    private readonly userService: UserService,
  ) {}

  // TODO: change logged user type
  async create(loggedUser: any, createCourseDto: CreateCourseDto): Promise<CourseDto> {
    const user = await this.userService.findOneBy({ email: loggedUser.email });
    let course = this.repo.create();

    course = {
      ...course,
      ...createCourseDto,
    }

    await this.repo.save(course);

    return new CourseDto(course);
  }

  async findAll(): Promise<CourseDto[]> {
    const courses = await this.repo.find();
    return courses.map((course) => new CourseDto(course));
  }

  async findOne(id: string): Promise<CourseDto> {
    const course = await this.repo.findOneBy({ id })
    return new CourseDto(course);
  }

  async update(id: string, updateCourseDto: UpdateCourseDto): Promise<CourseDto> {
    await this.repo.update(id, updateCourseDto);
    return await this.findOne(id);
  }

  async remove(id: string): Promise<CourseDto> {
    const course = await this.findOne(id);
    await this.repo.delete(id);
    return course;
  }
}
