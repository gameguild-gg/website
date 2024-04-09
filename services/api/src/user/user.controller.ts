import { Body, Controller, Get, Post } from "@nestjs/common";
import { UserService } from './user.service';
import { Auth } from "../auth/decorators/http.decorator";
import { UserEntity } from "./entities";
import { UserDto } from "../dtos/user/user.dto";
import { AuthUser } from "../auth/decorators/auth-user.decorator";
import { ApiOkResponse, ApiTags } from "@nestjs/swagger";
import { EnrollmentDto } from "../dtos/course/enrollment.dto";

@Controller('users')
@ApiTags('users')
export class UserController {
  
  constructor(private readonly userService: UserService) {}

  @Post("user/enroll-course")
  @Auth()
  @ApiOkResponse({ type: EnrollmentDto })
  public async enrollCourse(
    @Body() courseId: string,
    @AuthUser() user: UserEntity,
  ): Promise<EnrollmentDto> {
    return await this.userService.enrollCourse(user, courseId);
  }

}
