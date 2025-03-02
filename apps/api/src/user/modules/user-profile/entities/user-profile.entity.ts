import { Column, Entity, OneToOne } from 'typeorm';
import { UserEntity } from '@/user/entities/user.entity';
import { UserProfileDto } from '@/user/modules/user-profile/dtos/user-profile.dto';
import { DISPLAY_NAME_MAX_LENGTH, FAMILY_NAME_MAX_LENGTH, GIVEN_NAME_MAX_LENGTH } from '@/user/modules/user-profile/user-profile.constants';
import { ContentBase } from '@/common/entities/content.base';

@Entity({ name: 'user_profile' })
export class UserProfileEntity extends ContentBase implements UserProfileDto {
  @OneToOne(() => UserEntity, (user) => user.profile)
  public readonly user: UserEntity;

  @Column({ type: 'varchar', length: GIVEN_NAME_MAX_LENGTH, nullable: true, default: null })
  public readonly givenName?: string;

  @Column({ type: 'varchar', length: FAMILY_NAME_MAX_LENGTH, nullable: true, default: null })
  public readonly familyName?: string;

  @Column({ type: 'varchar', length: DISPLAY_NAME_MAX_LENGTH, nullable: true, default: null })
  public readonly displayName?: string;
}

//   //
//   //   @Column({ nullable: true, default: null, type: 'varchar', length: 256 })
//   //   @ApiProperty()
//   //   @IsOptional()
//   //   @IsString({ message: 'error.IsString: bio should be a string' })
//   //   @MaxLength(256, {
//   //     message: 'error.MaxLength: bio should be at most 256 characters',
//   //   })
//   //   bio?: string;
//   //
//   //   // todo: make this a virtual column and concatenate given and family name
//   //   @Column({ nullable: true, default: null, type: 'varchar', length: 256 })
//   //   @ApiProperty()
//   //   @IsOptional()
//   //   @IsString({ message: 'error.IsString: location should be a string' })
//   //   @MaxLength(256, {
//   //     message: 'error.MaxLength: location should be at most 256 characters',
//   //   })
//   //   name?: string;
//   //

//   //
//   //   // picture is a relationship to an asset
//   //   @ApiProperty()
//   //   @IsOptional()
//   //   @OneToOne(() => ImageEntity)
//   //   @JoinColumn()
//   //   picture?: ImageEntity;
// }
