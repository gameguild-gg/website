import { ApiProperty } from '@nestjs/swagger';

export class UserProfilePatchDto {

  @ApiProperty({type: 'string'})
  readonly picture?: string;

  @ApiProperty({type: 'string'})
  readonly name?: string;

  @ApiProperty({type: 'string'})
  readonly bio?: string;
}
