import {Inject, Injectable} from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import type { TypeOrmModuleOptions } from '@nestjs/typeorm';
import { isNil } from 'lodash';

// import { UserSubscriber } from '../../entity-subscribers/user-subscriber';
import { SnakeNamingStrategy } from '../snake-naming.strategy';
import path from "path";

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

    private getNumber(key: string): number {
        const value = this.get(key);

        try {
            return Number(value);
        } catch {
            throw new Error(key + ' environment variable is not a number');
        }
    }

    private getBoolean(key: string, defaultValue: boolean): boolean {
        const value = this.get(key, defaultValue.toString());

        try {
            return Boolean(JSON.parse(value));
        } catch {
            throw new Error(key + ' env var is not a boolean');
        }
    }

    private getString(key: string, defaultValue: string = ''): string {
        const value = this.get(key, defaultValue);

        return value.replace(/\\n/g, '\n');
    }

    get nodeEnv(): string {
        return this.getString('NODE_ENV') || 'development';
    }

    get fallbackLanguage(): string {
        return this.getString('FALLBACK_LANGUAGE') || 'en';
    }

    get postgresConfig(): TypeOrmModuleOptions {
        let entities = [
            path.join(__dirname, '/../modules/**/*.entity{.ts,.js}'),
            path.join(__dirname, '/../modules/**/*.view-entity{.ts,.js}'),
        ];
        let migrations = [path.join(__dirname, '/../migrations/*{.ts,.js}')];

        return {
            entities,
            migrations,
            keepConnectionAlive: !this.isTest,
            dropSchema: this.isTest,
            type: 'postgres',
            name: 'default',
            host: this.getString('DB_HOST') || 'postgres',
            port: this.getNumber('DB_PORT') || 5432,
            username: this.getString('DB_USERNAME') || 'postgres',
            password: this.getString('DB_PASSWORD') || 'postgres',
            database: this.getString('DB_DATABASE') || 'postgres',
            // subscribers: [UserSubscriber],
            migrationsRun: true,
            logging: this.getBoolean('ENABLE_ORM_LOGS', true),
            namingStrategy: new SnakeNamingStrategy(),
        };
    }

    // get awsS3Config() {
    //     return {
    //         bucketRegion: this.getString('AWS_S3_BUCKET_REGION'),
    //         bucketApiVersion: this.getString('AWS_S3_API_VERSION'),
    //         bucketName: this.getString('AWS_S3_BUCKET_NAME'),
    //     };
    // }

    get documentationEnabled(): boolean {
        return this.getBoolean('ENABLE_DOCUMENTATION', true);
    }

    // get natsEnabled(): boolean {
    //     return this.getBoolean('NATS_ENABLED', false);
    // }

    // get natsConfig() {
    //     return {
    //         host: this.getString('NATS_HOST'),
    //         port: this.getNumber('NATS_PORT'),
    //     };
    // }

    get authConfig() {
        return {
            privateKey: this.getString('JWT_PRIVATE_KEY'),
            publicKey: this.getString('JWT_PUBLIC_KEY'),
            jwtExpirationTime: this.getNumber('JWT_EXPIRATION_TIME'),
        };
    }

    get appConfig() {
        return {
            port: this.getString('PORT', "8080"),
        };
    }

    private get(key: string, defaultValue: string = ''): string {
        const value = this.service.get<string>(key, defaultValue);

        if (isNil(value)) {
            throw new Error(key + ' environment variable does not set'); // probably we should call process.exit() too to avoid locking the service
        }

        return value;
    }
}