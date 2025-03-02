import { Body, Controller, Get, Logger, Param, Put } from '@nestjs/common';
import { ApiResponse, ApiTags } from '@nestjs/swagger';
import { UserProfileDto } from '@/user/modules/user-profile/dtos/user-profile.dto';
import { UserProfileService } from '@/user/modules/user-profile/services/user-profile.service';

@ApiTags('users')
@Controller('users')
export class UserProfileController {
  private readonly logger = new Logger(UserProfileService.name);

  constructor(private readonly service: UserProfileService) {}

  @Get('profiles')
  @ApiResponse({ type: UserProfileDto, isArray: true })
  public async findAll() {}

  @Get(':username/profile')
  @ApiResponse({ type: UserProfileDto })
  public async findOne(@Param('username') username: string) {}

  @Put(':username/profile')
  @ApiResponse({ type: UserProfileDto })
  public async update(@Param('username') username: string, @Body() data: Partial<UserProfileDto>) {}
}
