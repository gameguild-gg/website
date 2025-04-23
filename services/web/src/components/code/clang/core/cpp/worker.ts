import { API } from './api';

let port: MessagePort | null = null;
let sharedApi: API | null = null;

const onAnyMessage = async (event) => {
  if (event.data.id === 'constructor') {
    try {
      port = event.data.data;
      port.onmessage = onAnyMessage;

      // If we already have a shared API instance, set its message port
      if (sharedApi) {
        sharedApi.setMessagePort(port);
      }
    } catch (error) {
      console.error('Error in message port initialization:', error);
      // Don't throw here as it would crash the worker
    }
  }
};

export function getSharedApi(): API | null {
  return sharedApi;
}

export function setSharedApi(api: API): void {
  try {
    sharedApi = api;
    // Set the port on the API if it exists
    if (port) {
      api.setMessagePort(port);
    }
  } catch (error) {
    console.error('Error setting shared API:', error);
    // Don't rethrow as this is called from the main thread
  }
}

self.addEventListener('message', onAnyMessage);
