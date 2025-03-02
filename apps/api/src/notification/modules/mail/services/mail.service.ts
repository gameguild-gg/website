// import { Injectable, Logger } from '@nestjs/common';
// import sendGridEmail from '@sendgrid/mail';
// import { ApiConfigService } from '../../../common/config.service';
//
// @Injectable()
// export class MailService {
//   private readonly logger = new Logger(MailService.name);
//
//   constructor(config: ApiConfigService) {
//     sendGridEmail.setApiKey(config.sendGridApiKey);
//   }
//
//   public async send(data: {
//     to: string;
//     from: string;
//     subject: string;
//     text: string;
//     html: string;
//   }): Promise<void> {
//     await sendGridEmail.send({
//       to: data.to,
//       from: data.from,
//       subject: data.subject,
//       text: data.text,
//       html: data.html,
//     });
//   }
// }
