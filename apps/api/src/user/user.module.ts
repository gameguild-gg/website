import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { UserController } from '@/user/controllers/user.controller';
import { UserEntity } from '@/user/entities/user.entity';
import { UserProfileModule } from '@/user/modules/user-profile/user-profile.module';
import { UserService } from '@/user/services/user.service';
import { FindOneUserHandler } from '@/user/queries/find-one-user.handler';

@Module({
  imports: [TypeOrmModule.forFeature([UserEntity]), UserProfileModule],
  controllers: [UserController],
  providers: [UserService, FindOneUserHandler],
  exports: [UserService],
})
export class UserModule {}
