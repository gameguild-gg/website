import { Entity, OneToOne } from "typeorm";
import { EntityBase } from "../../../../common/entities/entity.base";
import { UserEntity } from "../../../entities/user.entity";


@Entity({ name: 'user_profile' })
export class UserProfileEntity extends EntityBase {

  @OneToOne(() => UserEntity, (user) => user.profile)
  user: UserEntity;
}