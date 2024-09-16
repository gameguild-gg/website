import {FetchHttpClientAdapter} from '@/lib/core/http/adapters/fetch-http-client-adapter';

export const httpClientFactory = () => new FetchHttpClientAdapter();
