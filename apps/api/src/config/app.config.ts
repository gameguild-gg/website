import { registerAs } from '@nestjs/config';
import { environment } from './environment.config';

export interface AppConfig {
  host?: string;
  port: number;
  isDevelopmentEnvironment: boolean;
  isProductionEnvironment: boolean;
  isDocumentationEnabled: boolean;
}

export const appConfig = registerAs('app', (): AppConfig => {
  const nodeEnvironment = environment.get('NODE_ENV', 'development');
  const isProductionEnvironment = nodeEnvironment === 'production';

  return {
    host: environment.get('HOST', 'localhost'),
    port: environment.getNumber('PORT', 4000),
    isDevelopmentEnvironment: !isProductionEnvironment,
    isProductionEnvironment: isProductionEnvironment,
    isDocumentationEnabled: environment.getBoolean('DOCUMENTATION_ENABLED', false),
  };
});
