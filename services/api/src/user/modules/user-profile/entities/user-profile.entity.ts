import { Column, Entity, OneToOne } from 'typeorm';
import { EntityBase } from '../../../../common/entities/entity.base';
import { UserEntity } from '../../../entities/user.entity';
import { ApiProperty } from '@nestjs/swagger';

@Entity({ name: 'user_profile' })
export class UserProfileEntity extends EntityBase {
  @OneToOne(() => UserEntity, (user) => user.profile)
  user: UserEntity;

  @Column({ nullable: true, default: null })
  @ApiProperty()
  bio?: string;

  @Column({ nullable: true, default: null })
  @ApiProperty()
  name?: string;

  @Column({ nullable: true, default: null })
  @ApiProperty()
  givenName?: string;

  @Column({ nullable: true, default: null })
  @ApiProperty()
  familyName?: string;

  @Column({ nullable: true, default: null })
  @ApiProperty()
  picture?: string;
}
