import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository, UpdateResult } from 'typeorm';
import { UserEntity } from './entities';
import { UserAlreadyExistsException } from './exceptions/user-already-exists.exception';
import { UserProfileEntity } from './modules/user-profile/entities/user-profile.entity';
import { CreateLocalUserDto } from '../dtos/user/create-local-user.dto';
import { promises } from 'fs-extra';

@Injectable()
export class UserService extends TypeOrmCrudService<UserEntity> {
  private readonly logger = new Logger(UserService.name);

  constructor(
    @InjectRepository(UserEntity)
    private readonly repository: Repository<UserEntity>,
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

  public async createOneWithWalletAddress(
    walletAddress: string,
  ): Promise<UserEntity> {
    let user = await this.findOneBy({ walletAddress: walletAddress });
    if (user) {
      return user;
    } else {
      user = await this.repository.save({
        walletAddress: walletAddress,
      });
      let profile = new UserProfileEntity();
      profile = await this.repository.save(profile);
      user.profile = profile;
      user = await this.repository.save(user);
      return user;
    }
  }

  public async save(user: Partial<UserEntity>) {
    return this.repository.save(user);
  }
}
