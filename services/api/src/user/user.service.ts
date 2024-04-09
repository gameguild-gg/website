import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository, UpdateResult } from 'typeorm';
import { UserEntity } from './entities';
import { UserAlreadyExistsException } from './exceptions/user-already-exists.exception';
import { UserProfileEntity } from './modules/user-profile/entities/user-profile.entity';
import { CreateLocalUserDto } from "../dtos/user/create-local-user.dto";
import { EnrollmentDto } from '../dtos/course/enrollment.dto';
import { ContentService } from '../cms/content.service';

@Injectable()
export class UserService extends TypeOrmCrudService<UserEntity> {
  private readonly logger = new Logger(UserService.name);

  constructor(
    @InjectRepository(UserEntity)
    private readonly repository: Repository<UserEntity>,
    private readonly contentService: ContentService,
  ) {
    super(repository);
  }

  // update
  async updateOneTypeorm(
    id: string,
    user: Partial<UserEntity>,
  ): Promise<UpdateResult> {
    return this.repository.update(id, user);
  }

  async isEmailTaken(email: string): Promise<boolean> {
    return !!(await this.findOne({ where: { email: email } }));
  }

  async isUsernameTaken(username: string): Promise<boolean> {
    return !!(await this.findOne({ where: { username: username } }));
  }

  async createOneWithEmailAndPassword(
    data: CreateLocalUserDto,
  ): Promise<UserEntity> {
    if (await this.isEmailTaken(data.email)) {
      throw new UserAlreadyExistsException(
        `The email '${data.email}' is already associated with an existing user.`,
      );
    }

    if (data.username && (await this.isUsernameTaken(data.username))) {
      throw new UserAlreadyExistsException(
        `The username '${data.username}' is already associated with an existing user.`,
      );
    }

    return this.repository.save({
      username: data.username,
      email: data.email,
      emailVerified: false,
      passwordHash: data.passwordHash,
      passwordSalt: data.passwordSalt,
    });
  }

  public async save(user: Partial<UserEntity>) {
    return this.repository.save(user);
  }

  public async enrollCourse(user: UserEntity, courseId: string): Promise<EnrollmentDto> {
    const course = await this.contentService.getCourse(user, courseId);
    if (!course) {
      // TODO: return "course does not exist" error
      return null;
    }
    user.enrolledCourses = [...user.enrolledCourses, course];
    user = await this.repository.save(user);
    return new EnrollmentDto({ ...user, ...course });
  }
}
