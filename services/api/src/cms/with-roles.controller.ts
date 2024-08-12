import { Body, Post } from '@nestjs/common';
import { Auth, AuthUser } from '../auth';
import { ContentUserRolesEnum } from '../auth/auth.enum';
import { WithRolesEntity } from '../auth/entities/with-roles.entity';
import { TransferOwnershipRequestDto } from './dtos/transfer-ownership.request.dto';
import { WithRolesService } from './with-roles.service';
import { UserEntity } from '../user/entities';
import { EditorRequestDto } from './dtos/editor-request.dto';

export class WithRolesController<T extends WithRolesEntity> {
  constructor(private service: WithRolesService<T>) {}

  // switch owner
  @Post('transfer-ownership')
  @Auth({ content: ContentUserRolesEnum.OWNER, entity: T })
  async switchOwner(
    @AuthUser() user: UserEntity,
    @Body() body: TransferOwnershipRequestDto,
  ) {
    return this.service.SwitchOwner(body.id, user.id, body.newUser.id);
  }

  // add editor
  @Post('add-editor')
  @Auth({ content: ContentUserRolesEnum.OWNER, entity: T })
  async addEditor(
    @AuthUser() user: UserEntity,
    @Body() body: EditorRequestDto,
  ) {
    return this.service.AddEditor(body.id, body.editor.id);
  }

  // remove editor
  @Post('remove-editor')
  @Auth({ content: ContentUserRolesEnum.OWNER, entity: T })
  async removeEditor(
    @AuthUser() user: UserEntity,
    @Body() body: EditorRequestDto,
  ) {
    return this.service.RemoveEditor(body.id, body.editor.id);
  }
}
