import { ApiProperty } from "@nestjs/swagger";
import { JobPostEntity } from "../entities/job-post.entity";
import { JobApplicationEntity } from "../entities/job-application.entity";
import { IsArray } from "class-validator";

export class JobPostWithApplicationsDto {
    // Job Post
    @ApiProperty()
    jobPost: JobPostEntity;
  
    // Applications
    @ApiProperty({ type: JobApplicationEntity, isArray:true })
    @IsArray({ message: 'error.IsArray: applications should be an array' })
    applications: JobApplicationEntity[];
  }