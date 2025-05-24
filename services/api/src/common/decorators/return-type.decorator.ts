import { SetMetadata } from '@nestjs/common';
// import all types from @nestjs/swagger
import {
  ApiResponse,
  ApiOkResponse,
  ApiCreatedResponse,
  ApiAcceptedResponse,
  ApiNoContentResponse,
  ApiMovedPermanentlyResponse,
  ApiFoundResponse,
  ApiBadRequestResponse,
  ApiUnauthorizedResponse,
  ApiTooManyRequestsResponse,
  ApiNotFoundResponse,
  ApiInternalServerErrorResponse,
  ApiBadGatewayResponse,
  ApiConflictResponse,
  ApiForbiddenResponse,
  ApiGatewayTimeoutResponse,
  ApiGoneResponse,
  ApiMethodNotAllowedResponse,
  ApiNotAcceptableResponse,
  ApiNotImplementedResponse,
  ApiPreconditionFailedResponse,
  ApiPayloadTooLargeResponse,
  ApiRequestTimeoutResponse,
  ApiServiceUnavailableResponse,
  ApiUnprocessableEntityResponse,
  ApiUnsupportedMediaTypeResponse,
  ApiDefaultResponse,
} from '@nestjs/swagger';
import { ApiResponseOptions } from '@nestjs/swagger';

export const Response = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};

export const OkResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiOkResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};

export const CreatedResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiCreatedResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};

export const AcceptedResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiAcceptedResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};

export const NoContentResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiNoContentResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const MovedPermanentlyResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiMovedPermanentlyResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const FoundResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiFoundResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const BadRequestResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiBadRequestResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const UnauthorizedResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiUnauthorizedResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const TooManyRequestsResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiTooManyRequestsResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const NotFoundResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiNotFoundResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const InternalServerErrorResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiInternalServerErrorResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const BadGatewayResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiBadGatewayResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const ConflictResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiConflictResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const ForbiddenResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiForbiddenResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const GatewayTimeoutResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiGatewayTimeoutResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const GoneResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiGoneResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const MethodNotAllowedResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiMethodNotAllowedResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const NotAcceptableResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiNotAcceptableResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const NotImplementedResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiNotImplementedResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const PreconditionFailedResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiPreconditionFailedResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const PayloadTooLargeResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiPayloadTooLargeResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const RequestTimeoutResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiRequestTimeoutResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const ServiceUnavailableResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiServiceUnavailableResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const UnprocessableEntityResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiUnprocessableEntityResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const UnsupportedMediaTypeResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiUnsupportedMediaTypeResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
export const DefaultResponse = (options?: ApiResponseOptions): MethodDecorator => {
  return (target, key, descriptor) => {
    ApiDefaultResponse(options)(target, key, descriptor);
    if ('type' in options) SetMetadata('returnType', options.type)(target, key, descriptor);
  };
};
