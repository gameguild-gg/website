// import { ApiProperty } from '@nestjs/swagger';
// import { IsEnum, IsNotEmpty, IsOptional, IsString, IsUrl, Length, MaxLength } from 'class-validator';
// import { CrudValidationGroups } from '@dataui/crud';
// import { IsSlug } from '../../common/decorators/isslug.decorator';
// import { VisibilityEnum } from '../entities/visibility.enum';
// import { ProjectEntity } from '../entities/project.entity';
//
// export class UpdateProjectDto
//   implements Omit<ProjectEntity, 'createdAt' | 'editors' | 'owner' | 'id' | 'updatedAt' | 'versions' | 'tickets' | 'thumbnail' | 'banner' | 'screenshots'>
// {
//   @ApiProperty()
//   @MaxLength(255, { message: 'error.maxLength: title is too long, max 255' })
//   @IsNotEmpty({
//     message: 'error.isNotEmpty: title is required',
//     groups: [CrudValidationGroups.CREATE],
//   })
//   @IsOptional({ groups: [CrudValidationGroups.UPDATE] })
//   @IsString({ message: 'error.isString: title must be a string' })
//   title: string;
//
//   @ApiProperty()
//   @Length(1, 1024, {
//     message: 'error.length: summary max 1024 characters',
//   })
//   @IsNotEmpty({ message: 'error.isNotEmpty: summary is required' })
//   @IsString({ message: 'error.isString: summary must be a string' })
//   summary: string;
//
//   @IsString({ message: 'error.isString: body must be a string' })
//   @Length(1, 1024 * 64, {
//     message: 'error.length: body max 64kb',
//   })
//   @IsNotEmpty({ message: 'error.isNotEmpty: body is required' })
//   @ApiProperty()
//   // todo: guarantee the type here, md or anything else
//   body: string;
//
//   @ApiProperty()
//   @IsSlug({ message: 'error.slug: slug field must be a valid slug' })
//   @Length(1, 255, {
//     message: 'error.maxLength: slug field is too long: max 255',
//   })
//   @IsNotEmpty({
//     message: 'error.notEmpty: slug is required',
//     groups: [CrudValidationGroups.CREATE],
//   })
//   // @IsOptional({ groups: [CrudValidationGroups.UPDATE] })
//   @IsString({ message: 'error.isString: slug must be a string' })
//   slug: string;
//
//   @ApiProperty({ enum: VisibilityEnum })
//   @IsOptional()
//   @IsEnum(VisibilityEnum, {
//     message: 'error.isEnum: visibility must be a valid enum',
//   })
//   visibility: VisibilityEnum;
// }
//
// // the generated type does not contain the decorators.
