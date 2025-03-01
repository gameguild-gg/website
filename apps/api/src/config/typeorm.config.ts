import { DataSource, DataSourceOptions } from 'typeorm';
import { TypeOrmModuleOptions } from '@nestjs/typeorm';
import { registerAs } from '@nestjs/config';
import { join } from 'path';
import { SnakeNamingStrategy } from '../database/strategies/snake-naming.strategy';
import { environment } from './environment.config';

const entities = [
  // entities files patterns
  join(__dirname, '../**/*.entity{.ts,.js}'),
  join(__dirname, '../**/*.view-entity{.ts,.js}'),
];

const migrations = [
  // migrations files patterns
  join(__dirname, '../database/migrations/*{.ts,.js}'),
];

const subscribers = [
  // subscribers files patterns
  join(__dirname, '../**/*.subscriber{.ts,.js}'),
];

function getDataSourceOptions(): DataSourceOptions {
  return {
    type: 'postgres',
    host: environment.getString('DB_HOST', 'localhost'),
    port: environment.getNumber('DB_PORT', 5432),
    database: environment.getString('DB_DATABASE', 'postgres'),
    username: environment.getString('DB_USERNAME', 'postgres'),
    password: environment.getString('DB_PASSWORD', 'postgres'),
    schema: environment.getString('DB_SCHEMA', 'public'),
    entities: entities,
    migrations: migrations,
    subscribers: subscribers,
    dropSchema: environment.getBoolean('DB_DROP_SCHEMA_ENABLED', false),
    logging: environment.getBoolean('DB_LOGGING_ENABLED', false),
    migrationsRun: environment.getBoolean('DB_MIGRATIONS_RUN_ENABLED', true),
    namingStrategy: new SnakeNamingStrategy(),
    useUTC: environment.getBoolean('DB_USE_UTC_ENABLED', true),
  };
}

export const dataSource = new DataSource(getDataSourceOptions());

export const typeormConfig = registerAs('database', (): TypeOrmModuleOptions => getDataSourceOptions());
