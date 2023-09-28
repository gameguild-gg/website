import { Injectable } from '@nestjs/common';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { UserEntity } from './user.entity';
import { UserExistsException } from '../../exceptions/user-exists.exception';
import { UserRoleEnum } from './user-role.enum';
import { UserNotFoundException } from '../../exceptions/user-not-found.exception';

@Injectable()
export class UserService extends TypeOrmCrudService<UserEntity> {
  constructor(
    @InjectRepository(UserEntity)
    public repository: Repository<UserEntity>, // todo: make it private!!!
  ) {
    super(repository);
  }

  async emailExists(email: string): Promise<boolean> {
    return !!(await this.findOne({
      where: {
        email,
      },
    }));
  }

  async createOneEmailPass(data: {
    email: string;
    passwordHash: string;
    passwordSalt: string;
  }): Promise<UserEntity> {
    if (await this.emailExists(data.email))
      throw new UserExistsException('Email already exists');

    const user = this.repository.save({
      email: data.email,
      passwordHash: data.passwordHash,
      passwordSalt: data.passwordSalt,
      role: UserRoleEnum.COMMON,
      emailValidated: false,
    });

    return user;
  }

  async markEmailAsValid(id: string): Promise<UserEntity> {
    let user = await this.findOne({
      where: { id },
    });
    if (user) {
      user.emailValidated = true;
      user = await this.repository.save(user);
      return user;
    } else throw new UserNotFoundException('User not found with id: ' + id);
  }
}
