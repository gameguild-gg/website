import { registerAs } from '@nestjs/config';
import { JwtModuleOptions } from '@nestjs/jwt';
import { REFRESH_TOKEN_STRATEGY_KEY } from '@/auth/auth.constants';

export const refreshTokenConfig = registerAs(
  REFRESH_TOKEN_STRATEGY_KEY,
  (): JwtModuleOptions => ({
    // global: true,
    privateKey: process.env.REFRESH_TOKEN_PRIVATE_KEY,
    publicKey: process.env.REFRESH_TOKEN_PUBLIC_KEY,
    signOptions: {
      algorithm: 'HS512',
      expiresIn: process.env.REFRESH_TOKEN_EXPIRATION_TIME,
    },
    verifyOptions: {
      algorithms: ['HS512'],
      ignoreExpiration: false,
    },
  }),
);
