import { Module } from '@nestjs/common';
import { ApiConfigService } from './api-config.service';
import { PrismaService } from './prisma.service';

@Module({
  providers: [ApiConfigService, PrismaService],
  exports: [ApiConfigService, PrismaService],
})
export class CommonModule {}
