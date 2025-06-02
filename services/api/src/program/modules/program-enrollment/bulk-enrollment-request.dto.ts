import { IsString, IsArray, IsOptional, IsEnum } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { ProgramRoleType } from '../../entities/enums';

export class BulkEnrollmentRequest {
  @ApiProperty({ description: 'ID of the program to enroll users in' })
  @IsString()
  programId: string;

  @ApiProperty({ description: 'Array of user product IDs to enroll', type: [String] })
  @IsArray()
  @IsString({ each: true })
  userProductIds: string[];

  @ApiProperty({ description: 'Role to assign to enrolled users', required: false, enum: ProgramRoleType })
  @IsOptional()
  @IsEnum(ProgramRoleType)
  defaultRole?: ProgramRoleType;
}
