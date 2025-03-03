import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

import { UserProfileController } from '@/user/modules/user-profile/controllers/user-profile.controller';
import { UserProfileEntity } from '@/user/modules/user-profile/entities/user-profile.entity';
import { UserProfileService } from '@/user/modules/user-profile/services/user-profile.service';

@Module({
  imports: [TypeOrmModule.forFeature([UserProfileEntity])],
  controllers: [UserProfileController],
  providers: [UserProfileService],
  exports: [UserProfileService],
})
export class UserProfileModule {}
