import { ApiProperty, ApiPropertyOptional, PickType } from "@nestjs/swagger";
import { ContentBaseDto } from "../content/content-base.dto";

export class FindCourseDto extends PickType(ContentBaseDto, ["type", "visibility"]) {
    @ApiProperty()
    id: string;

    @ApiPropertyOptional()
    price: number;

    @ApiProperty()
    subscriptionAccess: boolean;

    // social tags. todo: create a tag entity
}
