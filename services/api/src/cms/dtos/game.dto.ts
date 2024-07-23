import { PermissionsDto, WithRolesDto } from '../../auth/dtos/with-roles.dto';
import { ContentBaseDto } from './content.base.dto';
import { ApiProperty } from '@nestjs/swagger';
import { GameVersionDto } from './game-version.dto';
import { ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';

export class GameDto extends ContentBaseDto implements WithRolesDto {
  @ApiProperty({ type: () => PermissionsDto })
  @ValidateNested({ message: 'roles must be an instance of PermissionsDto' })
  @Type(() => PermissionsDto)
  roles: PermissionsDto;

  @ApiProperty({ type: () => GameVersionDto, isArray: true })
  @ValidateNested({
    each: true,
    message: 'versions must be an array of GameVersionDto',
  })
  @Type(() => GameVersionDto)
  versions: GameVersionDto[];
}
