export type HttpRequest = {
  url: string;
  method: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: any;
};
