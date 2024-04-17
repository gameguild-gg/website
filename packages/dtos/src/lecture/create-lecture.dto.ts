import { ApiProperty, ApiPropertyOptional } from "@nestjs/swagger";
import { ContentBase } from "../../cms/entities/content.base";

export class CreateLectureDto extends ContentBase {
    @ApiPropertyOptional()
    order: number;
    
    @ApiProperty()
    courseId: string;
    
    @ApiProperty()
    chapterId: string;
}
