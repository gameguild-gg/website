import { CreateManyDto, CrudController, CrudRequest, CrudService, GetManyDefaultResponse } from '@dataui/crud';
import { Body, Controller, Get, Patch, Post } from '@nestjs/common';
import { UserProfileEntity } from './entities/user-profile.entity';
import { ApiBody, ApiResponse, ApiTags, getSchemaPath } from '@nestjs/swagger';
import { Auth, AuthUser } from 'src/auth';
import { AuthenticatedRoute } from 'src/auth/auth.enum';
import { UserEntity } from 'src/user/entities';
import { UserProfileService } from './user-profile.service';
import { UserProfilePatchDto } from './dtos/user-profile-patch.dto';

@Controller('user-profile')
@ApiTags('User Profile')
export class UserProfileController{
  constructor(private readonly service: UserProfileService) {}
    
  @Get('me')
  @Auth(AuthenticatedRoute)
  @ApiResponse({ 
    type: UserProfileEntity,
    schema: { $ref: getSchemaPath(UserProfileEntity) },
    status: 200,
  })
  public async getCurrentUserProfile(
    @AuthUser() user: UserEntity,
  ): Promise<UserProfileEntity> {
    return this.service.getMe(user.id);
  }

  @Patch('me')
  @Auth(AuthenticatedRoute)
  @ApiBody({ type: UserProfilePatchDto })
  @ApiResponse({ 
    type: UserProfileEntity,
    schema: { $ref: getSchemaPath(UserProfileEntity) },
    status: 200,
  })
  public async patchCurrentUserProfile(
    @Body() userProfilePatchDto: UserProfilePatchDto,
    @AuthUser() user: UserEntity,
  ): Promise<UserProfileEntity> {
    console.log("patch userProfile:")
    console.log(userProfilePatchDto)
    return this.service.patchMe(user.id,userProfilePatchDto);
  }

}
