import {HttpResponse} from '@/lib/core/http/http-response';
import {HttpRequest} from '@/lib/core/http/http-request';

export type HttpClient<T = any> = {
  request: (data: HttpRequest) => Promise<HttpResponse<T>>;
};
