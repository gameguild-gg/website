import { Module } from '@nestjs/common';
import { UserProfileController } from './user-profile.controller';
import { UserProfileService } from './user-profile.service';
import { TypeOrmModule } from '@nestjs/typeorm';
import { UserProfileEntity } from './entities/user-profile.entity';
import { AssetModule, AssetService } from '../../../asset';

@Module({
  imports: [TypeOrmModule.forFeature([UserProfileEntity]), AssetModule],
  controllers: [UserProfileController],
  providers: [UserProfileService],
  exports: [UserProfileService],
})
export class UserProfileModule {}
