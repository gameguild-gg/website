import { Resolver, Query, Args, ID } from '@nestjs/graphql';
import { Int } from '@nestjs/graphql';
import { CourseEntity } from '../cms/entities/course.entity';

@Resolver(() => CourseEntity)
export class CourseResolver {
  @Query(() => [CourseEntity], { name: 'courses' })
  async findAllCourses(): Promise<CourseEntity[]> {
    // TODO: Implement course service when available
    return [];
  }

  @Query(() => CourseEntity, { name: 'course', nullable: true })
  async findOneCourse(@Args('id', { type: () => ID }) id: string): Promise<CourseEntity | null> {
    // TODO: Implement course service when available
    return null;
  }

  @Query(() => CourseEntity, { name: 'courseBySlug', nullable: true })
  async findCourseBySlug(@Args('slug') slug: string): Promise<CourseEntity | null> {
    // TODO: Implement course service when available
    return null;
  }
}
