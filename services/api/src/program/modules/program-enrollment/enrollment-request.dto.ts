import { IsString, IsOptional, IsJSON } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';

export class EnrollmentRequest {
  @ApiProperty({ description: 'ID of the user product that grants access to the program' })
  @IsString()
  userProductId: string;

  @ApiProperty({ description: 'ID of the program to enroll in' })
  @IsString()
  programId: string;

  @ApiProperty({ description: 'Initial analytics data', required: false })
  @IsOptional()
  @IsJSON()
  analytics?: object;

  @ApiProperty({ description: 'Initial grades data', required: false })
  @IsOptional()
  @IsJSON()
  grades?: object;

  @ApiProperty({ description: 'Initial progress data', required: false })
  @IsOptional()
  @IsJSON()
  progress?: object;
}
