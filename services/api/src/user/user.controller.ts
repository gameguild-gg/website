import { Controller, Get, Patch } from '@nestjs/common';
import { UserService } from './user.service';
import { ApiResponse, ApiTags } from '@nestjs/swagger';
import { Auth, AuthUser } from '../auth';
import { AuthenticatedRoute } from '../auth/auth.enum';
import { UserEntity } from './entities';

@Controller('users')
@ApiTags('users')
export class UserController {
  constructor(private readonly userService: UserService) {}

  @Get('me')
  @Auth(AuthenticatedRoute)
  @ApiResponse({ type: UserEntity })
  async get(@AuthUser() user: UserEntity) {
    return this.userService.findOne({
      where: { id: user.id },
      relations: { profile: true },
    });
  }
}
