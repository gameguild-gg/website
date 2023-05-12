import * as dotenv from 'dotenv';
import { DataSource } from 'typeorm';

import { SnakeNamingStrategy } from './src/snake-naming.strategy';
import * as path from "path";

dotenv.config();

export const dataSource: DataSource = new DataSource({
    type: 'postgres',
    host: process.env.DB_HOST || 'localhost',
    port: Number(process.env.DB_PORT) || 5432,
    username: process.env.DB_USERNAME || 'postgres',
    password: process.env.DB_PASSWORD || 'postgres',
    database: process.env.DB_DATABASE || 'postgres',
    namingStrategy: new SnakeNamingStrategy(),
    subscribers: [ path.join(__dirname, '/src/**/*.subscriber{.ts,.js}')],
    entities: [
        path.join(__dirname, 'src/**/*.entity{.ts,.js}'),
        path.join(__dirname, 'src/**/*.view-entity{.ts,.js}'),
    ],
    migrations: [path.join(__dirname, 'src/migrations/*{.ts,.js}')],
});