import { INestApplication, Logger } from '@nestjs/common';
import { DocumentBuilder, SwaggerModule } from '@nestjs/swagger';
import { CommonModule } from './common/common.module';
import { execSync } from 'child_process';
import { ApiConfigService } from './common/config.service';
import * as fs from 'node:fs';
import * as process from 'node:process';
import { spawn } from 'child_process';

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
    const generateClient = spawn('npm', ['run', 'openapigenerator'], {
      stdio: 'ignore',
      detached: true,
    });
    generateClient.unref();
    generateClient.on('close', (code) => {
      if (code === 0) {
        logger.log('Client generated successfully');
        fixOpenApiGeneratorPlus();
        logger.log('OpenApiPlus fixed');

        if (process.env.NODE_ENV === 'development') {
          try {
            logger.log('Installing apiclient in web');
            const installClient = spawn(
              'npm',
              ['run', 'install:apiclient', '--prefix', '../../'],
              { stdio: 'ignore', detached: true },
            );
            installClient.unref();
            installClient.on('close', (code) => {
              if (code === 0) {
                logger.log('apiclient installed successfully on web');
              } else {
                logger.error(JSON.stringify(code));
              }
            });
          } catch (e) {
            logger.error(JSON.stringify(e));
          }
        }
      } else {
        logger.error('Error generating API client.');
      }
    });
  } catch (e) {
    logger.error('Error generating API client.');
  }
  logger.verbose(
    `Documentation: http://localhost:${configService.appConfig.port}/documentation`,
  );
}
