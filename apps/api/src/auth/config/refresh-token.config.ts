import { registerAs } from '@nestjs/config';
import { JwtModuleOptions } from '@nestjs/jwt';

import { REFRESH_TOKEN_STRATEGY_KEY } from '@/auth/auth.constants';
import { parseAlgorithm } from '@/auth/utils';
import { environment } from '@/config/environment.config';

export const refreshTokenConfig = registerAs(
  REFRESH_TOKEN_STRATEGY_KEY,
  (): JwtModuleOptions => ({
    // global: true,
    privateKey: environment.getString('REFRESH_TOKEN_PRIVATE_KEY'),
    publicKey: environment.getString('REFRESH_TOKEN_PUBLIC_KEY'),
    signOptions: {
      algorithm: parseAlgorithm(environment.getString('REFRESH_TOKEN_ALGORITHM', 'RS256')),
      expiresIn: environment.getString('REFRESH_TOKEN_EXPIRATION_TIME'),
    },
    verifyOptions: {
      algorithms: [parseAlgorithm(environment.getString('REFRESH_TOKEN_ALGORITHM', 'RS256'))],
      ignoreExpiration: environment.getBoolean('REFRESH_TOKEN_IGNORE_EXPIRATION', false),
    },
  }),
);
