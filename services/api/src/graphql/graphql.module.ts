import { Module } from '@nestjs/common';
import { HelloResolver } from './hello.resolver';
import { UserResolver } from './user.resolver';
import { UserModule } from '../user/user.module';

@Module({
  imports: [UserModule],
  providers: [HelloResolver, UserResolver],
})
export class GraphqlModule {}
