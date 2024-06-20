import { Inject, Injectable } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import type { TypeOrmModuleOptions } from '@nestjs/typeorm';

import * as ormconfig from '../../ormconfig';

@Injectable()
export class ApiConfigService {
  constructor(@Inject(ConfigService) private service: ConfigService) {}

  get isDevelopment(): boolean {
    return this.nodeEnv === 'development';
  }

  get isProduction(): boolean {
    return this.nodeEnv === 'production';
  }

  get isTest(): boolean {
    return this.nodeEnv === 'test';
  }

  get nodeEnv(): string {
    return ormconfig.nodeEnv();
  }

  get fallbackLanguage(): string {
    return ormconfig.getEnvString('FALLBACK_LANGUAGE') || 'en';
  }

  get postgresConfig(): TypeOrmModuleOptions {
    // todo: can we remove this verbose code?
    return {
      type: 'postgres',
      host: ormconfig.ormconfig.host,
      port: ormconfig.ormconfig.port,
      username: ormconfig.ormconfig.username,
      password: ormconfig.ormconfig.password,
      database: ormconfig.ormconfig.database,
      entities: ormconfig.ormconfig.entities,
      migrations: ormconfig.ormconfig.migrations,
      subscribers: ormconfig.ormconfig.subscribers,
      keepConnectionAlive: ormconfig.ormconfig.keepConnectionAlive,
      dropSchema: ormconfig.ormconfig.dropSchema,
      migrationsRun: ormconfig.ormconfig.migrationsRun,
      logging: ormconfig.ormconfig.logging,
      namingStrategy: ormconfig.ormconfig.namingStrategy,
    };
  }

  get documentationEnabled(): boolean {
    return ormconfig.getEnvBoolean('ENABLE_DOCUMENTATION', true);
  }

  get authConfig() {
    return {
      googleClientId: ormconfig.getEnvString('GOOGLE_CLIENT_ID'),
      accessTokenPrivateKey: ormconfig.getEnvString('ACCESS_TOKEN_PRIVATE_KEY'),
      accessTokenPublicKey: ormconfig.getEnvString('ACCESS_TOKEN_PUBLIC_KEY'),
      accessTokenAlgorithm: ormconfig.getEnvString('ACCESS_TOKEN_ALGORITHM'),
      accessTokenExpiresIn: ormconfig.getEnvString(
        'ACCESS_TOKEN_EXPIRATION_TIME',
      ),
      refreshTokenPrivateKey: ormconfig.getEnvString(
        'REFRESH_TOKEN_PRIVATE_KEY',
      ),
      refreshTokenPublicKey: ormconfig.getEnvString('REFRESH_TOKEN_PUBLIC_KEY'),
      refreshTokenAlgorithm: ormconfig.getEnvString('REFRESH_TOKEN_ALGORITHM'),
      refreshTokenExpiresIn: ormconfig.getEnvString(
        'REFRESH_TOKEN_EXPIRATION_TIME',
      ),
      emailVerificationTokenPrivateKey: ormconfig.getEnvString(
        'EMAIL_VERIFICATION_TOKEN_PRIVATE_KEY',
      ),
      emailVerificationTokenPublicKey: ormconfig.getEnvString(
        'EMAIL_VERIFICATION_TOKEN_PUBLIC_KEY',
      ),
      emailVerificationTokenAlgorithm: ormconfig.getEnvString(
        'EMAIL_VERIFICATION_TOKEN_ALGORITHM',
      ),
      emailVerificationTokenExpiresIn: ormconfig.getEnvString(
        'EMAIL_VERIFICATION_TOKEN_EXPIRATION_TIME',
      ),
      emailVerificationUrl: ormconfig.getEnvString('EMAIL_VERIFICATION_URL'),
    };
  }

  get appConfig() {
    return {
      port: ormconfig.getEnvString('PORT', '8080'),
    };
  }

  get hostFrontendUrl(): string {
    return ormconfig.getEnvString('HOST_FRONT_URL');
  }

  get hostBackendUrl(): string {
    return ormconfig.getEnvString('HOST_BACK_URL');
  }

  // get awsS3Config() {
  //     return {
  //         bucketRegion: ormconfig.getEnvString('AWS_S3_BUCKET_REGION'),
  //         bucketApiVersion: ormconfig.getEnvString('AWS_S3_API_VERSION'),
  //         bucketName: ormconfig.getEnvString('AWS_S3_BUCKET_NAME'),
  //     };
  // }

  // get natsEnabled(): boolean {
  //     return this.getBoolean('NATS_ENABLED', false);
  // }

  // get natsConfig() {
  //     return {
  //         host: ormconfig.getEnvString('NATS_HOST'),
  //         port: this.getNumber('NATS_PORT'),
  //     };
  // }
}
