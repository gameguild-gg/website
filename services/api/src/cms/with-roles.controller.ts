import { Body, Post } from '@nestjs/common';
import { Auth, AuthUser, EntityClassWithRolesField } from '../auth';
import { ContentUserRolesEnum, OwnerRoute } from '../auth/auth.enum';
import { WithRolesEntity } from '../auth/entities/with-roles.entity';
import { TransferOwnershipRequestDto } from './dtos/transfer-ownership.request.dto';
import { WithRolesService } from './with-roles.service';
import { UserEntity } from '../user/entities';
import { EditorRequestDto } from './dtos/editor-request.dto';

export class WithRolesController<T extends WithRolesEntity = never> {
  constructor(private withRolesService: WithRolesService<T>) {}

  // switch owner
  @Post('transfer-ownership')
  @Auth<T>(OwnerRoute<T>)
  async switchOwner(
    @AuthUser() user: UserEntity,
    @Body() body: TransferOwnershipRequestDto,
  ) {
    return this.withRolesService.SwitchOwner(body.id, user.id, body.newUser.id);
  }

  // add editor
  @Post('add-editor')
  @Auth<T>(OwnerRoute<T>)
  async addEditor(
    @AuthUser() user: UserEntity,
    @Body() body: EditorRequestDto,
  ) {
    return this.withRolesService.AddEditor(body.id, body.editor.id);
  }

  // remove editor
  @Post('remove-editor')
  @Auth<T>(OwnerRoute<T>)
  async removeEditor(
    @AuthUser() user: UserEntity,
    @Body() body: EditorRequestDto,
  ) {
    return this.withRolesService.RemoveEditor(body.id, body.editor.id);
  }
}
