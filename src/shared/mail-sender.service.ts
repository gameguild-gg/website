import {Injectable, Logger} from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { MailService } from "@sendgrid/mail";
import { MailDataRequired } from "@sendgrid/helpers/classes/mail";
import { ClientResponse } from "@sendgrid/client/src/response";

@Injectable()
export class MailSenderService {
    mailService: MailService;
    private readonly logger = new Logger(MailSenderService.name);
    private isSetup = false;

    constructor(private readonly configService: ConfigService) {
        // Don't forget this one.
        // The apiKey is required to authenticate our
        // request to SendGrid API.
        let sendgridKey = configService.get<string>('SENDGRID_API_KEY');
        if (sendgridKey) {
            this.mailService = new MailService();
            this.mailService.setApiKey(sendgridKey);
            this.isSetup = true;
        }
        else
            this.logger.error("SendGrid API Key not found in environment variables");
    }

    async send(mail: MailDataRequired): Promise<[ClientResponse, {}]> {
        return this.mailService.send(mail);
    }
}