import { ApiProperty } from '@nestjs/swagger';
import {
  IsArray,
  IsNotEmpty,
  IsString,
  IsUUID,
  ValidateNested,
} from 'class-validator';
import { Type } from 'class-transformer';

export type PermissionsType = {
  owner: string;
  editors?: string[];
};

export class PermissionsDto implements PermissionsType {
  @ApiProperty()
  @IsString({ message: 'permission must be a string' })
  @IsNotEmpty({ message: 'owner is required' })
  @IsUUID('4', { message: 'owner must be a valid UUID v4' })
  owner: string;

  @ApiProperty()
  @IsArray({ message: 'editors must be an array or strings' })
  @IsString({ each: true, message: 'each editor entry must be a string' })
  @IsUUID('4', {
    each: true,
    message: 'editors must be an array of valid UUID v4',
  })
  editors?: string[];
}

export class WithRolesType {
  roles: PermissionsType;
}

export class WithRolesDto implements WithRolesType {
  @ApiProperty({ type: () => PermissionsDto })
  @ValidateNested()
  @Type(() => PermissionsDto)
  roles: PermissionsDto;
}
