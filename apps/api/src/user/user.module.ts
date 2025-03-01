import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { UserEntity } from './entities/user.entity';
import { UserProfileModule } from './modules/user-profile/user-profile.module';
import { UserController } from './user.controller';
import { UserService } from './user.service';
import { AssetModule } from '../asset';

@Module({
  imports: [
    TypeOrmModule.forFeature([UserEntity]),
    UserProfileModule,
    AssetModule,
  ],
  controllers: [UserController],
  providers: [UserService],
  exports: [UserService],
})
export class UserModule {}
