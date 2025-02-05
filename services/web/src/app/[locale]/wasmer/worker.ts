import { expose } from 'comlink';
import { init, Output, Wasmer } from '@wasmer/sdk';
import { WasmerWorkerAPI } from '@/app/[locale]/wasmer/wasmer.api';

let wasmer: Wasmer | null = null;
let pkg: Wasmer | null = null;
let loadedPackage: string | null = null;

const initWasmerInstance = async () => {
  if (!wasmer) {
    try {
      await init({ sdkUrl: 'https://unpkg.com/@wasmer/sdk@latest/dist/index.mjs' });
      wasmer = Wasmer;
      return true;
    } catch (error: any) {
      throw new Error('Wasmer failed to initialize: ' + error.message);
    }
  }
  return true;
};

const fromRegistry = async (registry: string) => {
  if (!wasmer) {
    throw new Error('Wasmer has not been initialized yet.');
  }
  try {
    console.log(`Baixando pacote do Wasmer Registry: ${registry}`);
    pkg = await wasmer.fromRegistry(registry);

    if (!pkg) {
      throw new Error(`Falha ao carregar o pacote ${registry}`);
    }
    console.log('Detalhes do EntryPoint:', JSON.stringify(pkg.entrypoint));

    if (pkg.entrypoint) {
      try {
        // importScripts(pkg.entrypoint);
        console.log('EntryPoint carregado com sucesso no Web Worker!');
      } catch (error) {
        console.error('Erro ao carregar o EntryPoint no Web Worker:', error);
      }
    }

    loadedPackage = registry;
    return true;
  } catch (error: any) {
    console.error('Erro ao baixar o pacote do Wasmer Registry:', error);
    throw new Error('Failed to load package from registry: ' + error.message);
  }
};

const getLoadedPackage = () => {
  return loadedPackage;
};

const run = async (args: string[], onComplete: (result: Output) => void): Promise<void> => {
  if (!pkg) {
    console.error('Erro: Nenhum pacote foi carregado antes da execução.');
    return Promise.reject(new Error('Package is not loaded.'));
  }

  try {
    console.log('Executando código com os argumentos:', args);

    const instance = await pkg.entrypoint.run({ args });

    const result = await instance.wait(); // Aguarda a execução e obtém o resultado
    console.log('Execução concluída com sucesso:', result);
    onComplete(result); // Chama o callback SOMENTE em sucesso
  } catch (error: any) {
    console.error('Erro ao executar o código no Wasmer:', error);
    return Promise.reject(error); // Rejeita a Promise em caso de erro
  }
};

const workerApi: WasmerWorkerAPI = {
  init: initWasmerInstance,
  fromRegistry,
  getLoadedPackage,
  run,
};

expose(workerApi);
