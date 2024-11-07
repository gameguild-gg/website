import { Inject, Injectable } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import type { TypeOrmModuleOptions } from '@nestjs/typeorm';

import * as ormconfig from '../../ormconfig';

// import dotenv from 'dotenv';
import * as dotenv from 'dotenv';
import * as process from 'node:process';
import { decodeBase64 } from 'ethers';

@Injectable()
export class ApiConfigService {
  constructor(@Inject(ConfigService) private service: ConfigService) {
    // todo: god forgive me for what I am doing now.
    // note: This is a workaround to mimic the next dotenv behavior.
    // for some reason the app module is not able to read .env.local files, so I am rewriting everything here.
    // if you found a better solution to this, please let me know on gh: tolstenko

    const processEnv = process.env;
    const defaultConfig =
      dotenv.config({ path: '.env', override: false }).parsed || {};
    const localConfig =
      dotenv.config({ path: '.env.local', override: false }).parsed || {};
    const testConfig =
      dotenv.config({ path: '.env.test', override: false }).parsed || {};
    const productionConfig =
      dotenv.config({ path: '.env.production', override: false }).parsed || {};
    const developmentConfig =
      dotenv.config({ path: '.env.development', override: false }).parsed || {};

    // merge all envs
    // default is overridden by proccess
    // process is overridden by local
    let env = { ...defaultConfig, ...processEnv, ...localConfig };

    if (!env.NODE_ENV || env.NODE_ENV === 'development') {
      env.NODE_ENV = 'development';
    }

    if (env.NODE_ENV === 'test') {
      env = { ...env, ...testConfig };
    }

    if (env.NODE_ENV === 'production') {
      env = { ...env, ...productionConfig };
    }

    if (env.NODE_ENV === 'development') {
      env = { ...env, ...developmentConfig };
    }

    // override process env
    Object.keys(env).forEach((key) => {
      process.env[key] = env[key];
    });
  }

  get sendGridApiKey(): string {
    return this.service.getOrThrow<string>('SENDGRID_API_KEY');
  }
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
    return ormconfig.getEnvString('FALLBACK_LANGUAGE', 'en');
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
      entities: [...ormconfig.ormconfig.entities],
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
    const decoder = new TextDecoder();
    return {
      googleClientId: ormconfig.getEnvString('GOOGLE_CLIENT_ID'),
      accessTokenPrivateKey: decoder.decode(
        decodeBase64(ormconfig.getEnvString('ACCESS_TOKEN_PRIVATE_KEY')),
      ),
      accessTokenPublicKey: decoder.decode(
        decodeBase64(ormconfig.getEnvString('ACCESS_TOKEN_PUBLIC_KEY')),
      ),
      accessTokenAlgorithm: ormconfig.getEnvString('ACCESS_TOKEN_ALGORITHM'),
      accessTokenExpiresIn: ormconfig.getEnvString(
        'ACCESS_TOKEN_EXPIRATION_TIME',
        '15m',
      ),
      refreshTokenPrivateKey: decoder.decode(
        decodeBase64(ormconfig.getEnvString('REFRESH_TOKEN_PRIVATE_KEY')),
      ),
      refreshTokenPublicKey: decoder.decode(
        decodeBase64(ormconfig.getEnvString('REFRESH_TOKEN_PUBLIC_KEY')),
      ),
      refreshTokenAlgorithm: ormconfig.getEnvString('REFRESH_TOKEN_ALGORITHM'),
      refreshTokenExpiresIn: ormconfig.getEnvString(
        'REFRESH_TOKEN_EXPIRATION_TIME',
        '365d',
      ),
      // emailVerificationTokenPrivateKey: ormconfig.getEnvString(
      //   'EMAIL_VERIFICATION_TOKEN_PRIVATE_KEY',
      // ),
      // emailVerificationTokenPublicKey: ormconfig.getEnvString(
      //   'EMAIL_VERIFICATION_TOKEN_PUBLIC_KEY',
      // ),
      // emailVerificationTokenAlgorithm: ormconfig.getEnvString(
      //   'EMAIL_VERIFICATION_TOKEN_ALGORITHM',
      // ),
      // emailVerificationTokenExpiresIn: ormconfig.getEnvString(
      //   'EMAIL_VERIFICATION_TOKEN_EXPIRATION_TIME',
      // ),
      // emailVerificationUrl: ormconfig.getEnvString('EMAIL_VERIFICATION_URL'),
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

  get assetSourceInfoMap(): Map<string, SourceInfo> {
    // iterate over all env vars and get all env vars beginning with ASSET_SOURCE_.
    // `ASSET_SOURCE_XXXX_TYPE` - Source type (s3, IPFS, Cloudinary...)
    // `ASSET_SOURCE_XXXX_ENDPOINT` - Source server API URL
    // `ASSET_SOURCE_XXXX_KEY` - Source key, email or username if applicable
    // `ASSET_SOURCE_XXXX_SECRET` - Source password or secret if applicable
    // `ASSET_SOURCE_XXXX_BUCKET` - Source bucket or path if applicable
    // where XXXX is the source name

    const sources: Map<string, SourceInfo> = {};

    // get all env vars
    const env = process.env;

    // iterate over all env vars
    Object.keys(env).forEach((key: string) => {
      // check if the env var starts with ASSET_SOURCE_
      if (key.startsWith('ASSET_SOURCE_')) {
        // get the source name
        const sourceName = key.replace('ASSET_SOURCE_', '');
        // get the source type
        const type = env[`ASSET_SOURCE_${sourceName}_TYPE`];
        // get the source endpoint
        const endpoint = env[`ASSET_SOURCE_${sourceName}_ENDPOINT`];
        // get the access key
        const accessKey = env[`ASSET_SOURCE_${sourceName}_ACCESS_KEY`];
        // get the source secret
        const secretKey = env[`ASSET_SOURCE_${sourceName}_SECRET_KEY`];
        // get the source bucket
        const bucket = env[`ASSET_SOURCE_${sourceName}_BUCKET`];

        // add the source to the sources object
        sources[sourceName] = {
          type,
          endpoint,
          accessKey,
          secretKey,
          bucket,
        };
      }
    });

    return sources;
  }
}

export type SourceInfo = {
  type: string;
  endpoint: string;
  accessKey: string;
  secretKey: string;
  bucket: string;
};
