import { Body, Controller, Get, Patch, UploadedFile } from '@nestjs/common';
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
  @ApiResponse({ type: UserProfileEntity })
  async update(
    @AuthUser() user: UserEntity,
    @Body() body: UpdateUserProfileDto,
  ): Promise<UserProfileEntity> {
    const profile = await this.service.repository.findOne({
      where: { user: { id: user.id } },
      select: { id: true },
    });
    await this.service.repository.update(profile.id, body);
    return this.service.repository.findOne({ where: { id: profile.id } });
  }

  @Get('me')
  @Auth(AuthenticatedRoute)
  @ApiResponse({ type: UserProfileEntity })
  async get(@AuthUser() user: UserEntity) {
    return this.service.repository.findOne({
      where: { user: { id: user.id } },
    });
  }

  @Patch('profile-picture')
  @Auth(AuthenticatedRoute)
  @ApiFile({
    acceptedFileExtensions: ['jpg', 'jpeg', 'png'],
    fieldOptions: { maxCount: 1, fieldName: 'profilePicture' },
    maxFileSize: 1024 * 1024 * 2,
  })
  @ApiResponse({ type: UserProfileEntity })
  async updateProfilePicture(
    @AuthUser() user: UserEntity,
    @UploadedFile() file: Express.Multer.File,
  ) {
    return this.service.updateProfilePicture(user, file);
  }
}
