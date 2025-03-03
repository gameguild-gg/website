import { registerAs } from '@nestjs/config';
import { JwtModuleOptions } from '@nestjs/jwt';
import { EMAIL_VERIFICATION_TOKEN_STRATEGY_KEY } from '@/auth/auth.constants';
import { environment } from '@/config/environment.config';
import { parseAlgorithm } from '@/auth/utils';

export const emailVerificationTokenConfig = registerAs(
  EMAIL_VERIFICATION_TOKEN_STRATEGY_KEY,
  (): JwtModuleOptions => ({
    // global: true,
    privateKey: environment.getString('EMAIL_VERIFICATION_TOKEN_PRIVATE_KEY'),
    publicKey: environment.getString('EMAIL_VERIFICATION_TOKEN_PUBLIC_KEY'),
    signOptions: {
      algorithm: parseAlgorithm(environment.getString('EMAIL_VERIFICATION_TOKEN_ALGORITHM', 'RS256')),
      expiresIn: environment.getString('EMAIL_VERIFICATION_TOKEN_EXPIRATION_TIME'),
    },
    verifyOptions: {
      algorithms: [parseAlgorithm(environment.getString('EMAIL_VERIFICATION_TOKEN_ALGORITHM', 'RS256'))],
      ignoreExpiration: environment.getBoolean('EMAIL_VERIFICATION_TOKEN_IGNORE_EXPIRATION', false),
    },
  }),
);
