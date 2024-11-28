import { Body, Controller, Patch } from '@nestjs/common';
import { UserProfileService } from './user-profile.service';
import { Auth, AuthUser } from '../../../auth';
import { AuthenticatedRoute } from '../../../auth/auth.enum';
import { UserEntity } from '../../entities';
import { UpdateUserProfileDto } from './dtos/update-user-profile.dto';

@Controller('user-profile')
export class UserProfileController {
  constructor(private service: UserProfileService) {}

  // update user profile
  @Patch()
  @Auth(AuthenticatedRoute)
  async update(
    @AuthUser() user: UserEntity,
    @Body() data: UpdateUserProfileDto,
  ) {
    return this.service.repository.update(user, data);
  }

  async updateProfilePicture() {}
}
