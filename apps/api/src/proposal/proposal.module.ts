import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { ProposalEntity } from './entities/proposal.entity';
import { ProposalController } from './proposal.controller';
import { ProposalService } from './proposal.service';

@Module({
  imports: [TypeOrmModule.forFeature([ProposalEntity])],
  controllers: [ProposalController],
  providers: [ProposalService],
})
export class ProposalModule {}
