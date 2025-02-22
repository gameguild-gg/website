import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { ChoiceCastController } from './choicecasts/choice-casts.controller';
import { ChoiceCastService } from './choicecasts/choice-cast.service';
import { ChoiceCastScheduler } from './choicecasts/choice-cast-scheduler.service';
import { ChoiceCastEntity } from './choicecasts/entities/choice-cast.entity';

@Module({
  imports: [
    TypeOrmModule.forFeature([ChoiceCastEntity])
  ],
  controllers: [ChoiceCastController],
  providers: [
    ChoiceCastService,
    ChoiceCastScheduler,
  ],
})
export class EventModule {}
