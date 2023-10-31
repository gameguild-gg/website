import { Injectable, Logger } from '@nestjs/common';
import * as sendGridEmail from '@sendgrid/mail';

@Injectable()
export class MailService {
  private readonly logger = new Logger(MailService.name);

  constructor() {
    sendGridEmail.setApiKey(process.env.SENDGRID_API_KEY);
  }


}
