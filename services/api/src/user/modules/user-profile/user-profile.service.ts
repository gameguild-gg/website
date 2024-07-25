import { Injectable, Logger } from '@nestjs/common';
import { UserProfileEntity } from './entities/user-profile.entity';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';

@Injectable()
export class UserProfileService {
  private readonly logger = new Logger(UserProfileService.name);

  constructor(
    @InjectRepository(UserProfileEntity)
    private readonly repository: Repository<UserProfileEntity>,
  ) {}
  // create
  async save(profile: Partial<UserProfileEntity>): Promise<UserProfileEntity> {
    return new UserProfileEntity(await this.repository.save(profile));
  }
}
