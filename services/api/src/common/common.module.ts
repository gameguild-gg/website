import { Global, Module } from '@nestjs/common';
import { ApiConfigService } from "./config.service";

@Global()
@Module({
  providers: [ApiConfigService],
  exports: [ApiConfigService],
})
export class CommonModule {}
