// todo: move most of this file to ApiConfigService

import * as dotenv from 'dotenv';
import * as path from 'path';
import { DataSource } from 'typeorm';

import { SnakeNamingStrategy } from './src/snake-naming.strategy';

import { isNil } from 'lodash';

dotenv.config();

export function getEnv(key: string, defaultValue: string = null): string {
  // todo: return to use the nest service. it is broken!!!
  // dotenv can be unsafe
  //const value = service.get<string>(key, defaultValue);
  const value = process.env[key] || defaultValue;

  if (isNil(value) || value === '') {
    throw new Error(key + ' environment variable does not set. Manually set it or talk with us to provide them to you.'); // probably we should call process.exit() too to avoid locking the service
  }

  return value;
}

export function getEnvString(key: string, defaultValue: string = null): string {
  const value = getEnv(key, defaultValue);

  return value.replace(/\\n/g, '\n');
}

export function nodeEnv(): string {
  return getEnvString('NODE_ENV', 'development');
}

export function isTest(): boolean {
  return nodeEnv() === 'test';
}

export function getEnvNumber(key: string, defaultValue: number = null): number {
  const value = getEnv(key, defaultValue?.toString());

  try {
    return Number(value);
  } catch {
    throw new Error(key + ' environment variable is not a number');
  }
}

export function getEnvBoolean(key: string, defaultValue: boolean = null): boolean {
  const value = getEnv(key, defaultValue.toString());

  try {
    return Boolean(JSON.parse(value));
  } catch {
    throw new Error(key + ' env var is not a boolean');
  }
}

function getconfig() {
  const entities = [
    path.join(__dirname, 'src/**/*.entity{.ts,.js}'),
    path.join(__dirname, 'src/**/*.view-entity{.ts,.js}'),
    // ...permissionEntities,
    // permissions
  ];
  const migrations = [path.join(__dirname, 'migrations/*{.ts,.js}')];
  const subscribers = [path.join(__dirname, 'src/**/*.subscriber{.ts,.js}')];

  return {
    entities,
    migrations,
    subscribers,
    dropSchema: isTest(),
    type: 'postgres',
    host: getEnvString('DB_HOST'),
    port: getEnvNumber('DB_PORT'),
    username: getEnvString('DB_USERNAME'),
    password: getEnvString('DB_PASSWORD'),
    database: getEnvString('DB_DATABASE'),
    migrationsRun: true,
    logging: getEnvBoolean('ENABLE_ORM_LOGS', false),
    namingStrategy: new SnakeNamingStrategy(),
  };
}

export const ormconfig = getconfig();

export const dataSource = new DataSource({
  type: 'postgres',
  entities: ormconfig.entities,
  migrations: ormconfig.migrations,
  subscribers: ormconfig.subscribers,
  // keepConnectionAlive: config.keepConnectionAlive,
  dropSchema: ormconfig.dropSchema,
  host: ormconfig.host,
  port: ormconfig.port,
  username: ormconfig.username,
  password: ormconfig.password,
  database: ormconfig.database,
  migrationsRun: ormconfig.migrationsRun,
  logging: ormconfig.logging,
  namingStrategy: ormconfig.namingStrategy,
});
