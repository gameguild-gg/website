import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { WithRolesEntity } from '../auth/entities/with-roles.entity';
import { Repository } from 'typeorm';
import { UserEntity } from '../user/entities';

export class WithRolesService<
  T extends WithRolesEntity,
> extends TypeOrmCrudService<T> {
  constructor(protected repo: Repository<T>) {
    super(repo);
  }

  async SwitchOwner(id: string, newOwner: UserEntity): Promise<void> {
    // new owner should be a previous editor
    const repoWithRoles = this.repo as Repository<WithRolesEntity>;
    const entity = await repoWithRoles.findOne({
      where: { id, editors: { id: newOwner.id } },
    });
    if (!entity.editors.includes(newOwner)) {
      throw new Error('New owner should be a previous editor');
    }
    await repoWithRoles.update({ id }, { owner: newOwner });
  }

  async AddEditor(id: string, newEditor: UserEntity): Promise<void> {
    await this.repo
      .createQueryBuilder()
      .relation(WithRolesEntity, 'editors')
      .of(id)
      .add(newEditor);
  }
}
