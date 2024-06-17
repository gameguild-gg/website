import { HttpClient, HttpRequest, HttpResponse } from '@/lib/core/http';

export class FetchHttpClientAdapter implements HttpClient {
  async request<T>(data: HttpRequest): Promise<HttpResponse<T>> {
    const response = await fetch(data.url, {
      method: data.method,
      body: data.body,
    });

    const body = await response.json();

    return {
      statusCode: response.status,
      body,
    };
  }
}
