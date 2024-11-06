import { Module, forwardRef } from '@nestjs/common';
import { UserProfileController } from './user-profile.controller';
import { UserProfileService } from './user-profile.service';
import { TypeOrmModule } from '@nestjs/typeorm';
import { UserProfileEntity } from './entities/user-profile.entity';
import { UserModule } from 'src/user/user.module';
import { UserEntity } from 'src/user/entities';

@Module({
  imports: [
    TypeOrmModule.forFeature([UserProfileEntity, UserEntity]),
    forwardRef(() => UserModule),
  ],
  controllers: [UserProfileController],
  providers: [UserProfileService],
  exports: [UserProfileService],
})
export class UserProfileModule {}
