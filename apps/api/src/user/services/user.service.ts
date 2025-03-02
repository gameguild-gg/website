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
}
