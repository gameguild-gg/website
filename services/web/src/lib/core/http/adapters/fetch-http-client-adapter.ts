import { HttpClient, HttpRequest, HttpResponse } from '@/lib/core/http';
import { auth } from '@/auth';

export const dynamic = 'force-dynamic';

export class FetchHttpClientAdapter implements HttpClient {
  async request<T>(data: HttpRequest): Promise<HttpResponse<T>> {
    const session = await auth();

    const response = await fetch(data.url, {
      method: data.method,
      body: data.body ? JSON.stringify(data.body) : undefined,
      headers: {
        Authorization: `Bearer ${session?.user?.accessToken}`,
        'Content-Type': 'application/json',
        ...data.headers,
      },
    });

    const body = await response.json();

    return {
      statusCode: response.status,
      body,
    };
  }
}
