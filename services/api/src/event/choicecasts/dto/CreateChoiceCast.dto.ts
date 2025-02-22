import { ApiProperty } from "@dataui/crud/lib/crud";
import { EventEntity } from "../../entities/event.entity";
import { IsEnum, IsNotEmpty, IsOptional, IsString, Length } from "class-validator";
import { IsSlug } from "src/common/decorators/isslug.decorator";
import { VisibilityEnum } from "src/cms/entities/visibility.enum";

export class CreateChoiceCastDto implements
    Omit<
        EventEntity,
        | 'createdAt'
        | 'editors'
        | 'owner'
        | 'id'
        | 'updatedAt'
        | 'thumbnail'
    > {
    @ApiProperty()
    @IsNotEmpty()
    @IsString()
    @Length(1, 255)
    @IsSlug()
    slug: string;

    @ApiProperty()
    @IsNotEmpty()
    @IsString()
    @Length(1, 255)
    title: string;

    @ApiProperty()
    @IsOptional()
    @IsString()
    @Length(1, 1024)
    summary: string;

    @ApiProperty()
    @IsOptional()
    @IsString()
    @Length(1, 1024 * 64)
    body: string;

    @ApiProperty({ enum: VisibilityEnum })
    @IsOptional()
    @IsEnum(VisibilityEnum)
    visibility: VisibilityEnum;
}
