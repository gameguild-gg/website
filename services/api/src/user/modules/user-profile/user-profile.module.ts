import { Module } from '@nestjs/common';
import { UserProfileController } from './user-profile.controller';
import { UserProfileService } from './user-profile.service';

@Module({
  controllers: [UserProfileController],
  providers: [UserProfileService]
})
export class UserProfileModule {}
