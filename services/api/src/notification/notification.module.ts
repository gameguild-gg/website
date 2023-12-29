import { Module } from '@nestjs/common';
import { MailModule } from './modules/mail/mail.module';
import { NotificationController } from './notification.controller';
import { NotificationService } from './notification.service';
import { PhoneModule } from './modules/phone/phone.module';

@Module({
  controllers: [NotificationController],
  providers: [NotificationService],
  imports: [MailModule, PhoneModule],
  exports: [NotificationService],
})
export class NotificationModule {}
