import { UserDto } from "../user/user.dto";
import { ApiProperty } from "@nestjs/swagger";

export class LocalSignInResponseDto {
  @ApiProperty()
  accessToken: string;
  @ApiProperty()
  refreshToken: string;
  @ApiProperty({ type: UserDto})
  user: UserDto;
}