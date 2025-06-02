import { ApiSchema } from '@nestjs/swagger';
import { EntityDto } from '@/common/dtos/entity.dto';
import { SerializedRule } from '@/common/dtos/serialized-rule';
import { UserDto } from '@/user/dtos/user.dto';

@ApiSchema({ name: 'Role' })
export class RoleDto extends EntityDto {
  public readonly name: string;
  public readonly rules: SerializedRule;
  public readonly users: UserDto[];
}
