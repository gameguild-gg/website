import {HttpStatusCode} from '@/lib/core/http/http-status-code';

export type HttpResponse<T = any> = {
  statusCode: HttpStatusCode;
  body?: T;
};
