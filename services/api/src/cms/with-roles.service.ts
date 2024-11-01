import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { WithRolesEntity } from '../auth/entities/with-roles.entity';
import {
  FindOptionsRelations,
  FindOptionsSelect,
  FindOptionsWhere,
  Repository,
} from 'typeorm';
import { QueryDeepPartialEntity } from 'typeorm/query-builder/QueryPartialEntity';

export class WithRolesService<
  T extends WithRolesEntity,
> extends TypeOrmCrudService<T> {
  constructor(protected repo: Repository<T>) {
    super(repo);
  }

  async SwitchOwner(
    id: string,
    oldOwnerId: string,
    newOwnerId: string,
  ): Promise<void> {
    // new owner should be a previous editor
    const entity = await this.repo.findOne({
      where: {
        id,
        owner: { id: oldOwnerId },
        editors: { id: newOwnerId },
      } as FindOptionsWhere<T>,
      relations: { editors: true } as FindOptionsRelations<T>,
      select: { id: true, editors: true, owner: true } as FindOptionsSelect<T>,
    });
    if (!entity) {
      throw new Error(
        'You should be the owner of the resource and the new owner should be a previous editor of the entity.',
      );
    }

    // set new owner, add the previous owner to editors if not already there
    await this.repo.update(id, {
      owner: { id: newOwnerId },
    } as unknown as QueryDeepPartialEntity<T>);

    await this.AddEditor(id, entity.owner.id);
  }

  async AddEditor(id: string, newEditorId: string): Promise<void> {
    // todo: waste of bandwith, we should consider a more efficient way to do this
    const entity = await this.repo.findOne({
      where: { id } as FindOptionsWhere<T>,
      relations: { editors: true } as FindOptionsRelations<T>,
      select: { id: true, editors: true } as FindOptionsSelect<T>,
    });
    if (!entity) {
      throw new Error('Entity not found.');
    }

    // add editor if not already there
    if (!entity.editors?.find((e) => e.id === newEditorId)) {
      await this.repo
        .createQueryBuilder()
        .relation(nameof<WithRolesEntity>((o:any) => o.editors))
        .of(id)
        .add(newEditorId);
    }
  }

  async RemoveEditor(id: string, editorId: string): Promise<void> {
    await this.repo
      .createQueryBuilder()
      .relation(nameof<WithRolesEntity>((o:any) => o.editors))
      .of(id)
      .remove(editorId);
  }

  async UserCanEdit(editorId: string, contentId: string): Promise<boolean> {
    // it should be owner or an editor
    return Boolean(
      await this.repo.findOne({
        where: [
          { id: contentId, owner: { id: editorId } },
          { id: contentId, editors: { id: editorId } },
        ] as FindOptionsWhere<T>[],
        select: { id: true } as FindOptionsSelect<T>,
      }),
    );
  }

  async getAllOwnedByMe(myId: string): Promise<void> {
    await this.repo.find({
      where: [
        {owner: { id: myId } },
      ] as FindOptionsWhere<T>[],
    })
  }

  async getAllEditableByMe(myId: string): Promise<void> {
    await this.repo.find({
      where: [
        {owner: { id: myId } },
        {editors: { id: myId } },
      ] as FindOptionsWhere<T>[],
    })
  }

}
