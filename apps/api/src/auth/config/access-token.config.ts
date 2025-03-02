import { registerAs } from '@nestjs/config';
import { JwtModuleOptions } from '@nestjs/jwt';
import { ACCESS_TOKEN_STRATEGY_KEY } from '@/auth/auth.constants';

export const accessTokenConfig = registerAs(
  ACCESS_TOKEN_STRATEGY_KEY,
  (): JwtModuleOptions => ({
    // global: true,
    privateKey: process.env.ACCESS_TOKEN_PRIVATE_KEY,
    publicKey: process.env.ACCESS_TOKEN_PUBLIC_KEY,
    signOptions: {
      algorithm: 'HS512',
      expiresIn: process.env.ACCESS_TOKEN_EXPIRATION_TIME,
    },
    verifyOptions: {
      algorithms: ['HS512'],
      ignoreExpiration: false,
    },
  }),
);
