import { API } from './api';

let api: API;
let port: MessagePort;

const apiOptions = {
  hostWrite(s: string) {
    port.postMessage({ id: 'write', data: s });
  },
};

let currentApp = null;

const onAnyMessage = async (event) => {
  switch (event.data.id) {
    case 'constructor':
      port = event.data.data;
      port.onmessage = onAnyMessage;
      api = new API(apiOptions);
      break;

    case 'setShowTiming':
      api.showTiming = event.data.data;
      break;

    case 'compileLinkRun':
      currentApp = await api.compileLinkRun(event.data.data);
      console.log(`finished compileLinkRun. currentApp = ${currentApp}.`);
      break;
  }
};

self.addEventListener('message', onAnyMessage);
