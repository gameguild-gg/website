import { ApiProperty } from "@dataui/crud/lib/crud";
import { ApiPropertyOptional } from "@nestjs/swagger";
import { ContentTypeEnum } from "../../cms/entities/content-type.enum";
import { VisibilityEnum } from "../../cms/entities/visibility.enum";

export class ContentBaseDto {
    @ApiPropertyOptional({ type: "string", minLength: 7, maxLength: 255, default: '' })
    slug: string;

    @ApiPropertyOptional({ type: "string", minLength: 7, maxLength: 1024, default: '' })
    title: string;

    @ApiPropertyOptional({ type: "string", minLength: 7, maxLength: 1024, default: '' })
    summary: string;
    
    // @ApiProperty({ type: 'jsonb', default: {}, nullable: true})
    // todo: create a better typing to this. avoid using object
    // body: object;
    
    @ApiProperty({type: 'enum', enum: VisibilityEnum, default: VisibilityEnum.DRAFT})
    visibility: VisibilityEnum;

    // asset image
    @ApiPropertyOptional({nullable: true, default: ''})
    thumbnail: string;
    
    // todo: create availability dates
    
    @ApiProperty({type: 'enum', enum: ContentTypeEnum, default: ContentTypeEnum.NONE})
    type: ContentTypeEnum;
    
    // todo: how to make this a generic link to any entity inherited from this class?
    // @OneToMany((type) => TagEntity, (tag) => tag.content)
    // tags: TagEntity[];
}