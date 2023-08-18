import {
  Controller,
  HttpCode,
  HttpStatus,
  NotImplementedException,
  Param,
  Post,
  Query,
  UseGuards,
} from '@nestjs/common';
import { Prisma } from '@prisma/client';
import { RequireRole } from './decorators/role.decorator';
import { PermissionGuard } from './guards/admin.guard';
import { UserRole } from './user-role.enum';

@Controller('users')
export class UsersController {
  @Post('update/:userId')
  @UseGuards(PermissionGuard)
  @RequireRole(UserRole.ADMIN)
  @HttpCode(HttpStatus.OK)
  public async updateUser(
    @Param('userId') userId: string,
    @Query() query: Prisma.UserUpdateInput,
  ) {
    throw new NotImplementedException();
  }
}
