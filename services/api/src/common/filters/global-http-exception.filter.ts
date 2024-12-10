import {
  ExceptionFilter,
  Catch,
  ArgumentsHost,
  HttpException,
  HttpStatus,
} from '@nestjs/common';
import { HttpAdapterHost } from '@nestjs/core';
import { ApiConfigService } from '../config.service';
import { ApiProperty } from '@nestjs/swagger';

export class ErrorMessage {
  @ApiProperty()
  target: object;
  @ApiProperty()
  property: string;
  @ApiProperty({
    type: 'array',
    items: {
      type: 'object',
      additionalProperties: {
        type: 'string',
      },
    },
  })
  constraints: Record<string, string>[];
}

// todo: improve error handling type
export class ApiErrorResponseDto {
  @ApiProperty()
  statusCode: number;
  @ApiProperty()
  timestamp: string;
  @ApiProperty()
  path: string;
  @ApiProperty()
  msg: string;
  @ApiProperty()
  name: string;
  @ApiProperty({ type: ErrorMessage, isArray: true })
  message: [ErrorMessage] | any;
  @ApiProperty()
  error: any;
  @ApiProperty()
  stack: any;
  @ApiProperty()
  raw: any;
}

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

    const responseBody: Partial<ApiErrorResponseDto> = {
      statusCode: httpStatus,
      timestamp: new Date().toISOString(),
      path: request.url,
      msg: exception.message,
      name: exception.name,
    };
    if (exception.getResponse) {
      const errorResponse = exception.getResponse();
      if (errorResponse && typeof errorResponse === 'object') {
        if ('message' in errorResponse) {
          responseBody.message = errorResponse['message'];
        }
        if ('error' in errorResponse) {
          responseBody.error = errorResponse['error'];
        }
      }
    }
    if (this.configService.nodeEnv === 'development') {
      // split the stack into an array of lines
      responseBody.stack = exception.stack
        .split('\n')
        .map((line) => line.trim());
      responseBody.raw = exception;
    }

    console.log(responseBody);

    response.status(httpStatus).json(responseBody);
  }
}
