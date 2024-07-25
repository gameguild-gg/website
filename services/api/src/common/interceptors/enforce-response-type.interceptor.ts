import {
  CallHandler,
  ExecutionContext,
  Injectable,
  NestInterceptor,
  InternalServerErrorException,
  Type,
} from '@nestjs/common';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

@Injectable()
export class EnforceResponseTypeInterceptor implements NestInterceptor {
  intercept(context: ExecutionContext, next: CallHandler): Observable<any> {
    return next.handle().pipe(
      map(async (data) => {
        if (this.isPlainClassOrArrayInstanceRecursive(data)) {
          throw new InternalServerErrorException(
            `Dev Error: Response type conversion failed for the return type of ${context.getHandler().name} function. Please check if the response type is correct, or manually type the response object. Don't forget the @Response() decorator and check if there are nested untyped objects.`,
          );
        }
        return data;
      }),
    );
  }

  // private getResponseType(context: ExecutionContext): Type | null {
  //   const handler = context.getHandler();
  //   const responseDecorator = Reflect.getMetadata('returnType', handler);
  //   return responseDecorator ? responseDecorator : null;
  // }

  private isPlainClassOrArrayInstanceRecursive(obj: any): boolean {
    if (
      obj &&
      typeof obj === 'object' &&
      (obj.constructor.name === 'Object' || obj.constructor.name === 'Array')
    ) {
      return true;
    }
    if (obj && (typeof obj === 'object' || typeof obj === 'function')) {
      if (Array.isArray(obj)) {
        for (const item of obj) {
          if (this.isPlainClassOrArrayInstanceRecursive(item)) {
            return true;
          }
        }
        return false;
      } else {
        for (const key in obj) {
          if (this.isPlainClassOrArrayInstanceRecursive(obj[key])) {
            return true;
          }
        }
        return false;
      }
    } else {
      return (
        obj === null ||
        obj === undefined ||
        ['Object', 'Array'].includes(obj.constructor.name)
      );
    }
  }
}
