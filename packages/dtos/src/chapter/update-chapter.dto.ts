import { ApiProperty } from "@nestjs/swagger";
import { ContentBaseDto } from "../content/content-base.dto";

export class UpdateChapterDto extends ContentBaseDto {
    @ApiProperty()
    id: string;

    
}
