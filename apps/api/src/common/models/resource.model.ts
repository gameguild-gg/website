import { ResourceDto } from '@/common/dtos/resource-dto';
import { ModelBase } from '@/common/models/model.base';
import { UserModel } from '@/user/model/user.model';

export abstract class ResourceModel extends ModelBase implements ResourceDto {
  public readonly owner: UserModel;
}
