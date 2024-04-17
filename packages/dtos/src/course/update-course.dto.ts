import { ApiProperty, ApiPropertyOptional } from "@nestjs/swagger";
import { ChapterEntity } from "../../cms/entities/chapter.entity";
import { LectureEntity } from "../../cms/entities/lecture.entity";
import { ContentBaseDto } from "../content/content-base.dto";

export class UpdateCourseDto extends ContentBaseDto {
    @ApiProperty()
    id: string;
    
    @ApiPropertyOptional()
    price: number;

    @ApiProperty()
    subscriptionAccess: boolean;

    // social tags. todo: create a tag entity

    // a course have many lectures
    @ApiProperty({ type: () => LectureEntity, isArray: true })
    lectures: LectureEntity[];

    // a course have many chapters
    @ApiProperty({ type: () => ChapterEntity, isArray: true })
    chapters: ChapterEntity[];
}
