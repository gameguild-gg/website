import { ApiProperty } from "@nestjs/swagger";

export class UpdateLectureDto {
    @ApiProperty()
    id: string;
}
