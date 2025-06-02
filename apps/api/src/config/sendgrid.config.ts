import { registerAs } from '@nestjs/config';

export const sendGridConfig = registerAs('sendGrid', () => ({
  apiKey: process.env.SENDGRID_API_KEY,
}));
