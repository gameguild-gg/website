import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
// import { CourseEntity } from './entities/course.entity';
// import { LectureEntity } from './entities/lecture.entity';
import { PostEntity } from './entities/post.entity';
import { UserService } from '../user/user.service';

@Injectable()
export class ContentService {
  private readonly logger = new Logger(ContentService.name);

  constructor(
    // @InjectRepository(CourseEntity)
    // private readonly courseRepository: Repository<CourseEntity>,
    // @InjectRepository(LectureEntity)
    // private readonly lectureRepository: Repository<LectureEntity>,
    @InjectRepository(PostEntity)
    private readonly postRepository: Repository<PostEntity>,
    private readonly userService: UserService,
  ) {}

  // async createEmptyCourse(user: UserEntity): Promise<CourseEntity> {
  //   let course = new CourseEntity();
  //   user = await this.userService.findOne({ where: { id: user.id } });
  //   course.owner = user;
  //   course.editors = [user];
  //   course = await this.courseRepository.save(course);
  //
  //   return this.courseRepository.findOne({
  //     where: { id: course.id },
  //   });
  // }
}
