import {NestFactory, Reflector} from '@nestjs/core';
import { AppModule } from './app.module';
import {setupSwagger} from "./setup-swagger";
import {NestExpressApplication} from "@nestjs/platform-express";
import {SharedModule} from "./shared/shared.module";
import {ApiConfigService} from "./shared/config.service";
import { ExpressAdapter } from '@nestjs/platform-express';
import { middleware as expressCtx } from 'express-ctx';
import {ClassSerializerInterceptor, HttpStatus, UnprocessableEntityException, ValidationPipe} from "@nestjs/common";

async function bootstrap(): Promise<NestExpressApplication> {
  const app = await NestFactory.create<NestExpressApplication>(
      AppModule,
      new ExpressAdapter(),
      { cors: false }
  );

  const configService = app.select(SharedModule).get(ApiConfigService);

  if (configService.documentationEnabled) {
    setupSwagger(app);
  }

  // app.enable('trust proxy'); // only if you're behind a reverse proxy (Heroku, Bluemix, AWS ELB, Nginx, etc)
  // app.use(helmet());
  //app.setGlobalPrefix('/api'); // use api as global prefix if you don't have subdomain
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

  app.use(expressCtx);

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
  console.info(
      `Frontend: http://localhost:${configService.appConfig.port}/`,
  );

  return app;
}

bootstrap();
