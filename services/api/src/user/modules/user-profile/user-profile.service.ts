import { Injectable, Logger } from '@nestjs/common';
import { UserProfileEntity } from './entities/user-profile.entity';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';

@Injectable()
export class UserProfileService extends TypeOrmCrudService<UserProfileEntity> {
  private readonly logger = new Logger(UserProfileService.name);

  constructor(
    @InjectRepository(UserProfileEntity)
    private readonly repository: Repository<UserProfileEntity>,
  ) {
    super(repository);
  }
  // create
  save(profile: Partial<UserProfileEntity>) {
    return this.repository.save(profile);
  }
}
