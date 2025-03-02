import { Body, Controller, Delete, Get, Logger, Param, Post, Put } from '@nestjs/common';
import { ApiResponse, ApiTags } from '@nestjs/swagger';
import { UserDto } from '@/user/dtos/user.dto';
import { CommandBus, QueryBus } from '@nestjs/cqrs';

@ApiTags('users')
@Controller('users')
export class UserController {
  private readonly logger = new Logger(UserController.name);

  constructor(
    private readonly commandBus: CommandBus,
    private readonly queryBus: QueryBus,
  ) {}

  @Post()
  @ApiResponse({ type: UserDto })
  public async create() {}

  @Get()
  @ApiResponse({ type: UserDto, isArray: true })
  public async findAll() {}

  @Get(':username')
  @ApiResponse({ type: UserDto })
  public async findOne(@Param('username') username: string) {}

  @Put(':username')
  @ApiResponse({ type: UserDto })
  public async update(@Param('username') username: string, @Body() data: Partial<UserDto>) {}

  @Delete(':username')
  public async remove(@Param('username') username: string) {}
}
