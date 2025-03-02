import { registerAs } from '@nestjs/config';
import { JwtModuleOptions } from '@nestjs/jwt';
import { ACCESS_TOKEN_STRATEGY_KEY } from '@/auth/auth.constants';
import { environment } from '@/config/environment.config';
import { parseAlgorithm } from '@/auth/utils';

export const accessTokenConfig = registerAs(
  ACCESS_TOKEN_STRATEGY_KEY,
  (): JwtModuleOptions => ({
    // global: true,
    privateKey: environment.getString('ACCESS_TOKEN_PRIVATE_KEY'),
    publicKey: environment.getString('ACCESS_TOKEN_PUBLIC_KEY'),
    signOptions: {
      algorithm: parseAlgorithm(environment.getString('ACCESS_TOKEN_ALGORITHM', 'RS256')),
      expiresIn: environment.getString('ACCESS_TOKEN_EXPIRATION_TIME'),
    },
    verifyOptions: {
      algorithms: [parseAlgorithm(environment.getString('ACCESS_TOKEN_ALGORITHM', 'RS256'))],
      ignoreExpiration: environment.getBoolean('ACCESS_TOKEN_IGNORE_EXPIRATION', false),
    },
  }),
);
