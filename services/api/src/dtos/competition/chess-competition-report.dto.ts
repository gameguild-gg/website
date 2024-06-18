import { UserDto } from "../user/user.dto";
import { ApiProperty } from "@nestjs/swagger";
import { EntityDto } from "../entity.dto";

export class CompetitionRunSubmissionReportDto extends EntityDto {
  @ApiProperty()
  winsAsP1: number;

  @ApiProperty()
  winsAsP2: number;

  @ApiProperty()
  totalWins: number;

  @ApiProperty()
  pointsAsP1: number;

  @ApiProperty()
  pointsAsP2: number;

  @ApiProperty()
  totalPoints: number;

  @ApiProperty({type: () => UserDto})
  user: UserDto;
}
