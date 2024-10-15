import { ApiProperty } from '@nestjs/swagger';
import { UserEntity } from 'src/user/entities';
import { CrudRequest } from '@dataui/crud';

export class JobPostWithAppliedRequestDto{
    
  // Req (CrudRequest)
  @ApiProperty({ required: true, default: {} })
  req: CrudRequest

  // User
  @ApiProperty({ required: false })
  user: UserEntity

}
