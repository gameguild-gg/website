import { Module, forwardRef } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { UserEntity } from './entities/user.entity';
import { UserProfileModule } from './modules/user-profile/user-profile.module';
import { UserController } from './user.controller';
import { UserService } from './user.service';
import { ContentModule } from '../cms/content.module';

@Module({
  imports: [
    TypeOrmModule.forFeature([UserEntity]), 
    UserProfileModule,
    forwardRef(() => ContentModule),
  ],
  controllers: [UserController],
  providers: [UserService],
  exports: [UserService],
})
export class UserModule {}
