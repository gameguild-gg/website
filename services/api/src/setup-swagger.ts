import { INestApplication, Logger } from '@nestjs/common';
import { DocumentBuilder, OpenAPIObject, SwaggerModule } from '@nestjs/swagger';
import { CommonModule } from './common/common.module';
import { execSync } from 'child_process';
import { ApiConfigService } from './common/config.service';
import * as fs from 'node:fs';
import * as process from 'node:process';
// fsp is a promisified version of fs
import { promises as fsp } from 'fs';

import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

const logger: Logger = new Logger('SetupSwagger');

async function replaceInFile(path: string, from: string, to: string) {
  const content = await fsp.readFile(path, 'utf8');
  const result = content.replace(from, to);
  await fsp.writeFile(path, result, 'utf8');
}

async function fixOpenApiGeneratorPlus() {
  await replaceInFile(
    process.cwd() + '/../../packages/apiclient/runtime.ts',
    'import "whatwg-fetch";',
    '',
  );
  await replaceInFile(
    process.cwd() + '/../../packages/apiclient/runtime.ts',
    'window.fetch',
    'fetch',
  );
  
  // Fix the multipart/form-data Content-Type header issue
  // This prevents the "Multipart: Boundary not found" error by allowing the browser to set the boundary
  try {
    const apiTsPath = process.cwd() + '/../../packages/apiclient/api.ts';
    const content = await fsp.readFile(apiTsPath, 'utf8');
    
    // Use a more precise regex to target only the Content-Type header for multipart/form-data
    const fixedContent = content.replace(
      /localVarHeaderParameter\.set\('Content-Type', 'multipart\/form-data'\);/g,
      "// localVarHeaderParameter.set('Content-Type', 'multipart/form-data'); // Commented out to let browser set boundary"
    );
    
    if (content !== fixedContent) {
      await fsp.writeFile(apiTsPath, fixedContent, 'utf8');
      logger.log('Fixed multipart/form-data Content-Type header in API client');
    } else {
      logger.log('No Content-Type headers needed fixing in API client');
    }
  } catch (e) {
    logger.error('Failed to fix multipart/form-data Content-Type header in API client');
    logger.error(e);
  }
}

async function SetupDevelopmentConfigs(document: OpenAPIObject) {
  await fsp.writeFile('./swagger-spec.json', JSON.stringify(document, null, 2));
  logger.log('Swagger documentation generated successfully');

  try {
    logger.verbose('Generating api client');
    await execAsync('npm run openapigenerator');
    logger.log('Client generated successfully');
    await fixOpenApiGeneratorPlus();
    logger.log('OpenApiPlus fixed');
  } catch (e) {
    logger.error(
      'Error generating client. Do you have installed java runtime?',
    );
  }

  if (process.env.NODE_ENV === 'development') {
    try {
      logger.verbose('Installing apiclient in web');
      await execAsync('npm run install:apiclient --prefix ../../');
      logger.log('apiclient installed successfully on web');

      logger.verbose('Generating dbml schema');
      await execAsync('npm run db:diagram');
      logger.log('DBML schema generated successfully');
    } catch (e) {
      logger.error(JSON.stringify(e));
    }
  }
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

  SetupDevelopmentConfigs(document).catch((e) => {
    logger.error(e);
  });

  logger.verbose(
    `Documentation: http://localhost:${configService.appConfig.port}/documentation`,
  );
}
