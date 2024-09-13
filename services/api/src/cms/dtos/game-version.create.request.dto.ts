import { ApiProperty } from '@nestjs/swagger';
import { ProjectVersionEntity } from '../entities/project-version.entity';
import { Type } from 'class-transformer';
import { IsNotEmpty, IsString, IsUUID, ValidateNested } from 'class-validator';

export class GameVersionCreateRequestDto {
  @ApiProperty({ description: 'The id of the game this version belongs to' })
  @IsNotEmpty({ message: 'error.IsNotEmpty: gameId should not be empty' })
  @IsString({ message: 'error.IsString: gameId should be string' })
  @IsUUID(4, { message: 'error.uuid: gameId must be uuid 4' })
  gameId: string;

  @ApiProperty({ type: () => ProjectVersionEntity })
  @ValidateNested()
  @Type(() => ProjectVersionEntity)
  version: ProjectVersionEntity;
}
