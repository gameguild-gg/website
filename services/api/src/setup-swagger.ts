import type { INestApplication } from '@nestjs/common';
import { DocumentBuilder, SwaggerModule } from '@nestjs/swagger';
import { CommonModule } from './common/common.module';

import { ApiConfigService } from './common/config.service';
import * as fs from 'node:fs';

export function setupSwagger(app: INestApplication): void {
  const documentBuilder = new DocumentBuilder().setTitle('API').addBearerAuth();

  if (process.env.API_VERSION) {
    documentBuilder.setVersion(process.env.API_VERSION);
  }

  const document = SwaggerModule.createDocument(app, documentBuilder.build());
  SwaggerModule.setup('documentation', app, document, {
    swaggerOptions: {
      persistAuthorization: true,
    },
  });
  const configService = app.select(CommonModule).get(ApiConfigService);

  fs.writeFileSync('./swagger-spec.json', JSON.stringify(document));

  console.info(
    `Documentation: http://localhost:${configService.appConfig.port}/documentation`,
  );
}
