import { INestApplication, Logger } from '@nestjs/common';
import { DocumentBuilder, SwaggerModule } from '@nestjs/swagger';
import { CommonModule } from './common/common.module';
import { execSync } from 'child_process';
import { ApiConfigService } from './common/config.service';
import * as fs from 'node:fs';
import * as process from 'node:process';

const logger: Logger = new Logger('SetupSwagger');

function replaceInFile(path: string, from: string, to: string) {
  const content = fs.readFileSync(path, 'utf8');
  const result = content.replace(from, to);
  fs.writeFileSync(path, result, 'utf8');
}

function fixOpenApiGeneratorPlus() {
  replaceInFile(
    process.cwd() + '/../../packages/apiclient/runtime.ts',
    'import "whatwg-fetch";',
    '',
  );
  replaceInFile(
    process.cwd() + '/../../packages/apiclient/runtime.ts',
    'window.fetch',
    'fetch',
  );
}

export function setupSwagger(app: INestApplication): void {
  const documentBuilder = new DocumentBuilder()
    .setTitle('gameguild.gg')
    .addBearerAuth();
  documentBuilder.addServer('http://localhost:8080');
  documentBuilder.addServer('https://api.gameguild.gg');

  if (process.env.API_VERSION) {
    documentBuilder.setVersion(process.env.API_VERSION);
  }

  logger.verbose('generating swagger documentation');
  const document = SwaggerModule.createDocument(app, documentBuilder.build());
  SwaggerModule.setup('documentation', app, document, {
    swaggerOptions: {
      persistAuthorization: true,
    },
  });
  const configService = app.select(CommonModule).get(ApiConfigService);

  fs.writeFileSync('./swagger-spec.json', JSON.stringify(document, null, 2));
  logger.log('Swagger documentation generated successfully');

  try {
    logger.log('Generating api client');
    execSync('npm run openapigenerator', { stdio: 'ignore' });
    logger.log('Client generated successfully');
    fixOpenApiGeneratorPlus();
    logger.log('OpenApiPlus fixed');
  } catch (e) {
    logger.error(
      'Error generating client. Do you have installed java runtime?',
    );
  }

  if (process.env.NODE_ENV === 'development') {
    try {
      logger.log('Installing apiclient in web');
      execSync('npm run install:apiclient --prefix ../../', {
        stdio: 'ignore',
      });
      logger.log('apiclient installed successfully on web');
    } catch (e) {
      logger.error(JSON.stringify(e));
    }
  }

  logger.verbose(
    `Documentation: http://localhost:${configService.appConfig.port}/documentation`,
  );
}
