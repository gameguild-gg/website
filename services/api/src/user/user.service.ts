import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository, UpdateResult } from 'typeorm';
import { UserEntity } from './entities';
import { UserAlreadyExistsException } from './exceptions/user-already-exists.exception';
import { UserProfileEntity } from './modules/user-profile/entities/user-profile.entity';
import { CreateLocalUserDto } from '../dtos/user/create-local-user.dto';
import { UserProfileService } from './modules/user-profile/user-profile.service';
import { TokenPayload } from 'google-auth-library';

@Injectable()
export class UserService extends TypeOrmCrudService<UserEntity> {
  private readonly logger = new Logger(UserService.name);

  constructor(
    @InjectRepository(UserEntity)
    private readonly repository: Repository<UserEntity>,
    private readonly profileService: UserProfileService,
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
    let user = await this.findOne({
      where: { walletAddress: walletAddress },
      relations: { profile: true },
    });
    if (user) {
      return user;
    } else {
      user = await this.repository.save({
        walletAddress: walletAddress,
      });
      const profile = new UserProfileEntity();
      profile.user = user;
      await this.profileService.save(profile);

      return this.findOne({
        where: { walletAddress: walletAddress },
        relations: { profile: true },
      });
    }
  }

  public async save(user: Partial<UserEntity>) {
    return this.repository.save(user);
  }

  async createOneWithGoogleId(payload: TokenPayload): Promise<UserEntity> {
    let user = await this.findOne({
      where: { googleId: payload.sub },
      relations: { profile: true },
    });
    if (user) {
      // todo: update user profile with new data
      return user;
    } else {
      // todo: check the border case the user have previosly signed up with email and now wants to sign in with google
      // todo: relate to feature to merge accounts
      if (await this.isEmailTaken(payload.email)) {
        throw new UserAlreadyExistsException(
          `The email '${payload.email}' is already associated with an existing user. Merging accounts is not supported yet. Send us a message on Discord.`,
        );
      }
      user = await this.repository.save({
        googleId: payload.sub,
        email: payload.email,
        emailVerified: true,
      });
      const profile = new UserProfileEntity();
      profile.user = user;
      profile.name = payload.name;
      profile.givenName = payload.given_name;
      profile.familyName = payload.family_name;
      profile.picture = payload.picture;
      await this.profileService.save(profile);

      return this.findOne({
        where: { googleId: payload.sub },
        relations: { profile: true },
      });
    }
  }

  async createOneWithEmail(email: string) {
    // ensure the email is not already taken
    let user = await this.findOne({
      where: { email: email },
      relations: { profile: true },
    });
    if (user) {
      throw new UserAlreadyExistsException(
        `The email '${email}' is already associated with an existing user.`,
      );
    }
    user = await this.repository.save({
      email: email,
      emailVerified: false,
    });
    const profile = new UserProfileEntity();
    profile.user = user;
    await this.profileService.save(profile);
    return user;
  }
}
