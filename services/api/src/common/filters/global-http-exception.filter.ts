import {
  ExceptionFilter,
  Catch,
  ArgumentsHost,
  HttpException,
  HttpStatus,
} from '@nestjs/common';
import { HttpAdapterHost } from '@nestjs/core';
import { ApiConfigService } from '../config.service';

@Catch()
export class GlobalHttpExceptionFilter implements ExceptionFilter {
  constructor(
    private readonly httpAdapterHost: HttpAdapterHost,
    private readonly configService: ApiConfigService,
  ) {}

  catch(exception: HttpException, host: ArgumentsHost) {
    const ctx = host.switchToHttp();

    const httpStatus =
      exception instanceof HttpException
        ? exception.getStatus()
        : HttpStatus.INTERNAL_SERVER_ERROR;

    const response = ctx.getResponse();
    const request = ctx.getRequest<Request>();

    const errorResponse = exception.getResponse();

    const responseBody: any = {
      statusCode: httpStatus,
      timestamp: new Date().toISOString(),
      path: request.url,
      message: exception.message,
      error: exception.name,
    };
    if (errorResponse && typeof errorResponse === 'object') {
      if ('message' in errorResponse) {
        responseBody.message = errorResponse['message'];
      }
      if ('error' in errorResponse) {
        responseBody.error = errorResponse['error'];
      }
    }
    if (this.configService.nodeEnv === 'development') {
      // split the stack into an array of lines
      responseBody.stack = exception.stack
        .split('\n')
        .map((line) => line.trim());
      responseBody.exception = exception;
    }

    response.status(httpStatus).json(responseBody);
  }
}
