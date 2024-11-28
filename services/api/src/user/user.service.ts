import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository, Transaction, UpdateResult } from 'typeorm';
import { UserEntity } from './entities';
import { UserAlreadyExistsException } from './exceptions/user-already-exists.exception';
import { UserProfileEntity } from './modules/user-profile/entities/user-profile.entity';
import { CreateLocalUserDto } from '../dtos/user/create-local-user.dto';
import { UserProfileService } from './modules/user-profile/user-profile.service';
import { TokenPayload } from 'google-auth-library';

import { generateFromEmail, generateUsername } from 'unique-username-generator';

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
    return Boolean(await this.findOne({ where: { email: email } }));
  }

  async isUsernameTaken(username: string): Promise<boolean> {
    return Boolean(await this.findOne({ where: { username: username } }));
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

    if (!data.username) data.username = generateFromEmail(data.email, 8);

    return this.save({
      username: data.username,
      email: data.email,
      emailVerified: false,
      passwordHash: data.passwordHash,
      passwordSalt: data.passwordSalt,
      // todo: dont access the repository directly
      profile: await this.profileService.repository.save({}),
    });
  }

  public async createOneWithWalletAddress(
    walletAddress: string,
  ): Promise<UserEntity> {
    const user = await this.findOne({
      where: { walletAddress: walletAddress },
      relations: { profile: true },
    });
    if (user) {
      return user;
    } else {
      return await this.save({
        walletAddress: walletAddress,
        username: generateUsername('-', 8),
        // todo: dont access repository directly
        profile: await this.profileService.repository.save({}),
      });
    }
  }

  public async save(user: Partial<UserEntity>): Promise<UserEntity> {
    return this.repository.save(user);
  }

  async createOneWithGoogleId(payload: TokenPayload): Promise<UserEntity> {
    let user = await this.findOne({
      where: { googleId: payload.sub },
      relations: { profile: true },
    });
    if (user) {
      const toUpdate: Partial<UserEntity> = {};
      if (user.email !== payload.email) {
        toUpdate.email = payload.email;
      }
      if (!user.emailVerified) {
        toUpdate.emailVerified = true;
      }
      if (Object.keys(toUpdate).length > 0) {
        await this.updateOneTypeorm(user.id, toUpdate);
        user = await this.findOne({
          where: { googleId: payload.sub },
          relations: { profile: true },
        });

        // todo: merge user accounts if the email is already taken
      }
      return user;
    } else if (await this.isEmailTaken(payload.email)) {
      // todo: check the border case the user have previously signed up with email and now wants to sign in with google
      // todo: relate to feature to merge accounts
      throw new UserAlreadyExistsException(
        `The email '${payload.email}' is already associated with an existing user. Merging accounts is not supported yet. Send us a message on Discord.`,
      );
    }

    return this.save({
      googleId: payload.sub,
      email: payload.email,
      emailVerified: true,
      username: generateFromEmail(payload.email, 8),
      profile: await this.repository.save({
        name: payload.name,
        givenName: payload.given_name,
        familyName: payload.family_name,
        picture: payload.picture,
      } as UserProfileEntity),
    });
  }

  async createOneWithEmail(email: string): Promise<UserEntity> {
    // ensure the email is not already taken
    const user = await this.findOne({
      where: { email: email },
      relations: { profile: true },
    });
    if (user) {
      throw new UserAlreadyExistsException(
        `The email '${email}' is already associated with an existing user.`,
      );
    }
    return this.repository.save({
      email: email,
      emailVerified: false,
      username: generateFromEmail(email, 8),
      // todo: dont access the profile service directly
      profile: await this.profileService.repository.save({}),
    });
  }
}
