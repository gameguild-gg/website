import { Injectable, Logger } from '@nestjs/common';
import * as sendGridEmail from '@sendgrid/mail';

@Injectable()
export class MailService {
  private readonly logger = new Logger(MailService.name);

  constructor() {
    sendGridEmail.setApiKey(process.env.SENDGRID_API_KEY);
  }

  public async send(data: {
    to: string;
    from: string;
    subject: string;
    text: string;
    html: string;
  }): Promise<void> {
    await sendGridEmail.send({
      to: data.to,
      from: data.from,
      subject: data.subject,
      text: data.text,
      html: data.html,
    });
  }
}
