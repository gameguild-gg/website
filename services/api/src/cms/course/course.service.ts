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

  async create(createCourseDto: CreateCourseDto): Promise<CourseDto> {
    return null;
  }

  async findAll(): Promise<CourseDto[]> {
    return null;
  }

  async findOne(id: string): Promise<CourseDto> {
    return null;
  }

  async update(id: string, updateCourseDto: UpdateCourseDto): Promise<CourseDto> {
    return null;
  }

  async remove(id: string): Promise<CourseDto> {
    return null;
  }
}
