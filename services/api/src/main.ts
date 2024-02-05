import { ClassSerializerInterceptor, Logger } from '@nestjs/common';
import { NestFactory, Reflector } from '@nestjs/core';
import { AppModule } from './app.module';
import { CommonModule } from './common/common.module';
import { ApiConfigService } from './common/config.service';
import { setupSwagger } from './setup-swagger';

async function bootstrap() {
  const app = await NestFactory.create(AppModule, { cors: true });
  app.enableCors({
    origin: ['http://localhost:3000', "https://web.gameguild.gg"],
    methods: 'GET,HEAD,PUT,PATCH,POST,DELETE',
    preflightContinue: false,
    optionsSuccessStatus: 204,
  })
  const logger = new Logger();

  const configService = app.select(CommonModule).get(ApiConfigService);

  // app.enable('trust proxy'); // only if you're behind a reverse proxy (Heroku, Bluemix, AWS ELB, Nginx, etc)
  // app.use(helmet());
  // app.use(csurf());
  // app.use(
  //     rateLimit({
  //       windowMs: 15 * 60 * 1000, // 15 minutes
  //       max: 100, // limit each IP to 100 requests per windowMs
  //     }),
  // );
  // app.use(compression());
  // app.use(morgan('combined'));
  // app.enableVersioning();

  // const reflector = app.get(Reflector);

  // app.useGlobalFilters(
  //     new HttpExceptionFilter(reflector),
  //     new QueryFailedFilter(reflector),
  // );

  app.useGlobalInterceptors(
    new ClassSerializerInterceptor(app.get(Reflector)),
    // new TranslationInterceptor(
    //     app.select(SharedModule).get(TranslationService),
    // ),
  );

  // app.useGlobalPipes(
  //     new ValidationPipe({
  //       whitelist: true,
  //       errorHttpStatusCode: HttpStatus.UNPROCESSABLE_ENTITY,
  //       transform: true,
  //       dismissDefaultMessages: false,
  //       exceptionFactory: (errors) => new UnprocessableEntityException(errors),
  //     }),
  // );

  // only start nats if it is enabled
  // if (configService.natsEnabled) {
  //   const natsConfig = configService.natsConfig;
  //   app.connectMicroservice({
  //     transport: Transport.NATS,
  //     options: {
  //       url: `nats://${natsConfig.host}:${natsConfig.port}`,
  //       queue: 'main_service',
  //     },
  //   });
  //
  //   await app.startAllMicroservices();
  // }

  if (configService.documentationEnabled) {
    setupSwagger(app);
  }

  // Starts listening for shutdown hooks
  if (!configService.isDevelopment) {
    app.enableShutdownHooks();
  }

  const port = configService.appConfig.port;
  await app.listen(port);

  console.info(`server running on ${await app.getUrl()}`);
  console.info(
    `Documentation: http://localhost:${configService.appConfig.port}/documentation`,
  );

  // return app;
  // logger.log(`API server running on ${ await app.getUrl() }`);
}

bootstrap();
