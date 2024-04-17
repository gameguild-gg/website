import { ApiProperty, ApiPropertyOptional } from "@nestjs/swagger";
import { ContentBase } from "../../cms/entities/content.base";
import { LectureEntity } from "src/cms/entities/lecture.entity";

export class CreateChapterDto extends ContentBase {
    // TODO: check if this is optional
    @ApiPropertyOptional()
    order: number;
    
    @ApiProperty()
    courseId: string;
    
    @ApiProperty()
    lectures: LectureEntity[];
}
