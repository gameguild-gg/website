import { EntityDto } from '@/common/dtos/entity.dto';
import { UserDto } from '@/user/dtos/user.dto';

export class ResourceDto extends EntityDto {
  // DAC (Discretionary Access Control)

  public readonly owner: UserDto;

  // public readonly allowedUsers: UserDto[];

  // ABAC (Attribute-Based Access Control)

  // public readonly visibility: VisibilityStatus;
}
