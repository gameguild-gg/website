import { Module } from '@nestjs/common';
import { MailModule } from './modules/mail/mail.module';
import { NotificationController } from './notification.controller';
import { NotificationService } from './notification.service';

@Module({
  controllers: [NotificationController],
  providers: [NotificationService],
  imports: [MailModule]
})
export class NotificationModule {}
