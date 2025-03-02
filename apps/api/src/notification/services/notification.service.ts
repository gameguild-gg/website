// import { Injectable, Logger } from '@nestjs/common';
// import { MailService } from './modules/mail/mail.service';
// import { PhoneService } from './modules/phone/phone.service';
//
// @Injectable()
// export class NotificationService {
//   private readonly logger = new Logger(NotificationService.name);
//
//   constructor(
//     private readonly mailService: MailService,
//     private readonly phoneService: PhoneService,
//   ) {}
//
//   public async sendLocalNotification() {
//     // TODO: Implement this method.
//   }
//
//   public async sendEmailNotification(
//     email: string,
//     subject: string,
//     message: string,
//   ) {
//     await this.mailService.send({
//       to: email,
//       // from: 'no-reply@gameguild.gg',
//       subject: subject,
//       text: message,
//       html: message,
//     });
//   }
//
//   public async sendPhoneNotification() {
//     // TODO: Implement this method.
//     await this.phoneService.send();
//   }
// }
