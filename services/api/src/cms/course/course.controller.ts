import { Controller, Get, Post, Body, Patch, Param, Delete } from "@nestjs/common";
import { CourseService } from "./course.service";
import { CreateCourseDto } from "./dto/create-course.dto";
import { UpdateCourseDto } from "./dto/update-course.dto";
import { ApiTags } from "@nestjs/swagger";
import { CourseDto } from "./dto/course.dto";

@ApiTags("Course")
@Controller("course")
export class CourseController {
  constructor(private readonly courseService: CourseService) {}

  @Post("create-course")
  async create(@Body() createCourseDto: CreateCourseDto): Promise<CourseDto> {
    return await this.courseService.create(createCourseDto);
  }

  @Get("get-all-courses")
  async findAll(): Promise<CourseDto[]> {
    return await this.courseService.findAll();
  }

  @Get("get-couse/:id")
  async findOne(@Param("id") id: string): Promise<CourseDto> {
    return await this.courseService.findOne(id);
  }

  @Patch("update-course/:id")
  async update(@Param("id") id: string, @Body() updateCourseDto: UpdateCourseDto): Promise<CourseDto> {
    return await this.courseService.update(id, updateCourseDto);
  }

  @Delete("delete-course/:id")
  async remove(@Param("id") id: string): Promise<CourseDto> {
    return await this.courseService.remove(id);
  }
}
