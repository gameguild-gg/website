import type { INestApplication } from '@nestjs/common';
import { DocumentBuilder, SwaggerModule } from '@nestjs/swagger';
import {SharedModule} from "./shared/shared.module";
import {ApiConfigService} from "./shared/config.service";

export function setupSwagger(app: INestApplication): void {
    const documentBuilder = new DocumentBuilder()
        .setTitle('API')
        .addBearerAuth();

    if (process.env.API_VERSION) {
        documentBuilder.setVersion(process.env.API_VERSION);
    }

    const document = SwaggerModule.createDocument(app, documentBuilder.build());
    SwaggerModule.setup('documentation', app, document, {
        swaggerOptions: {
            persistAuthorization: true,
        },
    });
    const configService = app.select(SharedModule).get(ApiConfigService);

    console.info(
        `Documentation: http://localhost:${configService.appConfig.port}/documentation`,
    );
}