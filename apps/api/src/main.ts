import { ClassSerializerInterceptor, HttpStatus, INestApplication, Logger, UnprocessableEntityException, ValidationPipe } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { NestFactory, Reflector } from '@nestjs/core';
import { DocumentBuilder, SwaggerModule } from '@nestjs/swagger';
import compression from 'compression';
import cookieParser from 'cookie-parser';
import helmet from 'helmet';

import { AppModule } from './app.module';
import { AppConfig } from './config/app.config';

function setupGlobalFilters(app: INestApplication) {
  // TODO: Understand and configure the global filters.
  app.useGlobalFilters(); // ({
  //       new HttpExceptionFilter(reflector),
  //       new QueryFailedFilter(reflector),
  // });
}

function setupGlobalInterceptors(app: INestApplication) {
  // TODO: Understand and configure the global interceptors.
  app.useGlobalInterceptors(new ClassSerializerInterceptor(app.get(Reflector)));
}

function setupGlobalPipes(app: INestApplication) {
  // TODO: Understand and configure the global pipes.
  app.useGlobalPipes(
    new ValidationPipe({
      whitelist: true,
      errorHttpStatusCode: HttpStatus.UNPROCESSABLE_ENTITY,
      transform: true,
      transformOptions: {
        enableImplicitConversion: true,
      },
      dismissDefaultMessages: false,
      exceptionFactory: (errors) => new UnprocessableEntityException(errors),
    }),
  );
}

function setupSwagger(app: INestApplication) {
  const config = new DocumentBuilder().setTitle('Game Guild').setDescription('REST API Documentation').setVersion('0.1.0').addBearerAuth().build();

  const document = SwaggerModule.createDocument(app, config);

  SwaggerModule.setup('documentation', app, document);
}

export async function bootstrap(): Promise<INestApplication> {
  const app = await NestFactory.create(AppModule);

  const logger = new Logger('Bootstrap');

  const configService = app.get(ConfigService);
  const config = configService.get<AppConfig>('app');

  const host = config.host;
  const port = config.port;

  app.use(compression());
  app.use(cookieParser());
  // TODO: Understand and configure the helmet.
  app.use(helmet());

  // app.use(csurf());

  // TODO: Understand and configure the rate limit.
  // app.use(
  //     rateLimit({
  //       windowMs: 15 * 60 * 1000, // 15 minutes
  //       max: 100, // limit each IP to 100 requests per windowMs
  //     }),
  // );

  // app.use(morgan('combined'));

  // TODO: Understand and configure the cors.
  app.enableCors({
    methods: ['GET', 'HEAD', 'PUT', 'PATCH', 'POST', 'DELETE'],
    origin: ['http://localhost:3000', 'https://web.gameguild.gg', 'https://gameguild.gg'],
    optionsSuccessStatus: 204,
    preflightContinue: false,
  });

  // app.enable('trust proxy'); // only if you're behind a reverse proxy (Heroku, Bluemix, AWS ELB, Nginx, etc)

  // TODO: Understand and configure the versioning.
  // app.enableVersioning({ type: VersioningType.URI, defaultVersion: '1' });

  if (config.isDevelopmentEnvironment) {
    app.enableShutdownHooks();
  }

  setupGlobalFilters(app);
  setupGlobalInterceptors(app);
  setupGlobalPipes(app);

  if (config.isDocumentationEnabled) {
    setupSwagger(app);
  }

  // TODO: Understand and configure the microservices.
  // if (config.isMicroservicesEnabled) {
  //   for (const microServiceConfig of config.microservices) {
  //     app.connectMicroservice({
  //       transport: microServiceConfig.transport,
  //       options: microServiceConfig.options,
  //     });
  //   }
  //
  //   await app.startAllMicroservices();
  // }

  await app.listen(config.port, async () => {
    logger.verbose(`REST API is running on http://${host}:${port}/`);

    // if (config.isDocumentationEnabled) {
    logger.verbose(`REST API documentation is available at http://${host}:${port}/documentation`);
    // }
  });

  return app;
}

void bootstrap();
