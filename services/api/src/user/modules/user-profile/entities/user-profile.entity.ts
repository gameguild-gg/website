import { Column, Entity, OneToOne } from 'typeorm';
import { EntityBase } from '../../../../common/entities/entity.base';
import { UserEntity } from '../../../entities/user.entity';
import { ApiProperty } from '@nestjs/swagger';
import {
  IsArray,
  IsOptional,
  IsString,
  IsUrl,
  MaxLength,
  ValidateNested,
} from 'class-validator';
import { Type } from 'class-transformer';

@Entity({ name: 'user_profile' })
export class UserProfileEntity extends EntityBase {
  @OneToOne(() => UserEntity, (user) => user.profile)
  @ApiProperty({ type: () => UserEntity })
  @ValidateNested()
  @Type(() => UserEntity)
  user: UserEntity;

  @Column({ nullable: true, default: null, type: 'varchar', length: 256 })
  @ApiProperty()
  @IsOptional()
  @IsString({ message: 'error.IsString: bio should be a string' })
  @MaxLength(256, {
    message: 'error.MaxLength: bio should be at most 256 characters',
  })
  bio?: string;

  @Column({ nullable: true, default: null, type: 'varchar', length: 256 })
  @ApiProperty()
  @IsOptional()
  @IsString({ message: 'error.IsString: location should be a string' })
  @MaxLength(256, {
    message: 'error.MaxLength: location should be at most 256 characters',
  })
  name?: string;

  @Column({ nullable: true, default: null, type: 'varchar', length: 256 })
  @ApiProperty()
  @IsOptional()
  @IsString({ message: 'error.IsString: location should be a string' })
  @MaxLength(256, {
    message: 'error.MaxLength: location should be at most 256 characters',
  })
  givenName?: string;

  @Column({ nullable: true, default: null, type: 'varchar', length: 256 })
  @ApiProperty()
  @IsOptional()
  @IsString({ message: 'error.IsString: location should be a string' })
  @MaxLength(256, {
    message: 'error.MaxLength: location should be at most 256 characters',
  })
  familyName?: string;

  @Column({ nullable: true, default: null, type: 'varchar', length: 256 })
  @ApiProperty()
  @IsOptional()
  @IsString({ message: 'error.IsString: location should be a string' })
  @IsUrl({}, { message: 'error.IsUrl: picture should be a URL' })
  picture?: string;

  // constructor(partial?: Partial<UserProfileEntity>) {
  //   super(partial);
  //   Object.assign(this, partial);
  // }
}
