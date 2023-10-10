import {ApiProperty} from '@nestjs/swagger';
import {IsString} from 'class-validator';

export class CompetitionSubmissionDto {
    @ApiProperty({required: true})
    @IsString()
    username: string;

    @ApiProperty({type: 'string', format: 'number', required: false})
    @IsString()
    password: string;

    @ApiProperty({type: 'string', format: 'binary', required: true})
    file: Express.Multer.File;
}
