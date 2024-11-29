import {
  Body,
  Controller,
  Patch,
  UnprocessableEntityException,
  UploadedFile,
} from '@nestjs/common';
import { UserProfileService } from './user-profile.service';
import { Auth, AuthUser } from '../../../auth';
import { AuthenticatedRoute } from '../../../auth/auth.enum';
import { UserEntity } from '../../entities';
import { UpdateUserProfileDto } from './dtos/update-user-profile.dto';
import { ApiFile } from '../../../common/decorators/api-file.decorator';
import { ApiResponse, ApiTags } from '@nestjs/swagger';
import { UserProfileEntity } from './entities/user-profile.entity';

@Controller('user-profile')
@ApiTags('users')
export class UserProfileController {
  constructor(private service: UserProfileService) {}

  // update user profile
  @Patch('me')
  @Auth(AuthenticatedRoute)
  async update(
    @AuthUser() user: UserEntity,
    @Body() data: UpdateUserProfileDto,
  ) {
    return this.service.repository.update(user, data);
  }

  @Patch('profile-picture')
  @Auth(AuthenticatedRoute)
  @ApiFile({
    fileFilter(
      req: any,
      file: Express.Multer.File,
      callback: (error: Error | null, acceptFile: boolean) => void,
    ) {
      if (!file.mimetype.startsWith('image')) {
        return callback(
          new UnprocessableEntityException('Only images are allowed!'),
          false,
        );
      }
      return callback(null, true);
    },
  })
  @ApiResponse({ type: UserProfileEntity })
  async updateProfilePicture(
    @AuthUser() user: UserEntity,
    @UploadedFile() file: Express.Multer.File,
  ) {
    return this.service.updateProfilePicture(user, file);
  }
}
