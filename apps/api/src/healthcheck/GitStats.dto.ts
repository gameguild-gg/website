import { ApiProperty } from '@nestjs/swagger';

export class GitStats {
  @ApiProperty()
  username: string;
  @ApiProperty()
  additions: number;
  @ApiProperty()
  deletions: number;
}
