import { Module } from '@nestjs/common';
import { CommonModule } from 'src/common/common.module';
import { UsersService } from './users.service';
import { UsersController } from './users.controller';
import { PrismaService } from 'src/common/prisma.service';

@Module({
  imports: [CommonModule],
  providers: [UsersService, PrismaService],
  controllers: [UsersController],
  exports: [UsersService],
})
export class UsersModule {}
