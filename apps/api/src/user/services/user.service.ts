import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { Repository } from 'typeorm';
import { UserEntity } from '@/user/entities/user.entity';

@Injectable()
export class UserService extends TypeOrmCrudService<UserEntity> {
  private readonly logger = new Logger(UserService.name);

  constructor(@InjectRepository(UserEntity) private readonly repository: Repository<UserEntity>) {
    super(repository);
  }

  //
  // public async isEmailTaken(email: string): Promise<boolean> {
  //   return Boolean(await this.findOne({ where: { email: email } }));
  // }
  //
  // public async isUsernameTaken(username: string): Promise<boolean> {
  //   return Boolean(await this.findOne({ where: { username: username } }));
  // }
  //
  // public async createOneWithEmailAndPassword(data: CreateLocalUserDto): Promise<UserEntity> {
  //   if (await this.isEmailTaken(data.email)) {
  //     throw new UserAlreadyExistsException(`The email '${data.email}' is already associated with an existing user.`);
  //   }
  //
  //   if (data.username && (await this.isUsernameTaken(data.username))) {
  //     throw new UserAlreadyExistsException(`The username '${data.username}' is already associated with an existing user.`);
  //   }
  //
  //   // if (!data.username) data.username = generateFromEmail(data.email, 8);
  //
  //   return await this.repository.save({});
  //
  //   // return this.save({
  //   // //   username: data.username,
  //   //   email: data.email,
  //   //   emailVerified: false,
  //   // //   passwordHash: data.passwordHash,
  //   // //   passwordSalt: data.passwordSalt,
  //   // //   // todo: dont access the repository directly
  //   // //   profile: await this.profileService.repository.save({}),
  //   // });
  // }
}
