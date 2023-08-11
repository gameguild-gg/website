import { NestFactory, Reflector } from '@nestjs/core';
import {
  ClassSerializerInterceptor,
  INestApplication,
  ValidationPipe,
} from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { DocumentBuilder, SwaggerModule } from '@nestjs/swagger';
import { AppModule } from './app.module';

async function bootstrap() {
  const app = await NestFactory.create(AppModule, { cors: false });
  const configService = app.get(ConfigService);
  const reflector = app.get(Reflector);

  // Set global prefix for all routes.
  app.setGlobalPrefix('api');
  // app.use(helmet());
  // app.use(morgan('combined'));
  // app.use(compression());

  // app.use(rateLimit({ windowMs: 15 * 60 * 1000, max: 100 }));

  // app.enable('trust proxy'); // only if you're behind a reverse proxy (Heroku, Bluemix, AWS ELB, Nginx, etc).
  // app.enableVersioning();

  // Starts listening for shutdown hooks.
  // if (!configService.isDevelopment) {
  //   app.enableShutdownHooks();
  // }

  // const configService = app.select(CommonModule).get();

  // app.useGlobalFilters(
  //     new HttpExceptionFilter(reflector),
  //     new QueryFailedFilter(reflector),
  // );

  app.useGlobalInterceptors(
    new ClassSerializerInterceptor(reflector),
    //
  );

  app.useGlobalPipes(new ValidationPipe());

  // app.useGlobalPipes(
  //   new ValidationPipe({
  //     whitelist: true,
  //     errorHttpStatusCode: HttpStatus.UNPROCESSABLE_ENTITY,
  //     transform: true,
  //     dismissDefaultMessages: false,
  //     exceptionFactory: (errors) => new UnprocessableEntityException(errors),
  //   }),
  // );

  // only start nats if it is enabled.
  // if (configService.natsEnabled) {
  //   const natsConfig = configService.natsConfig;
  //   app.connectMicroservice({
  //     transport: Transport.NATS,
  //     options: {
  //       url: `nats://${natsConfig.host}:${natsConfig.port}`,
  //       queue: 'main_service',
  //     },
  //   });
  // }

  // await app.startAllMicroservices();

  // if(configService.IsDocumentationEnabled){
  if (configService.get<string>('DOCUMENTATION_ENABLED')) {
    const documentBuilder = new DocumentBuilder();

    documentBuilder.setTitle('API');

    if (configService.get<string>('API_VERSION')) {
      documentBuilder.setVersion(configService.get<string>('API_VERSION'));
    }

    const document = SwaggerModule.createDocument(app, documentBuilder.build());

    SwaggerModule.setup('documentation', app, document);
  }

  // const port = configService.api.port;
  //const port = configService.get<string>('PORT');

  await app.listen(3000);

  // console.info(`Web server running on ${await app.getUrl()}`);
  // console.info(`API Documentation: ${await app.get}/documentation`);
  // console.info(``);
}

bootstrap();
