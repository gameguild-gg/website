'use client';
import * as Comlink from 'comlink';
import Emception from './emception';
import { useEffect, useState } from 'react';

type EmceptionWrapper = {
  worker: Comlink.Remote<Emception> | null;
};

export default async function Page() {
  const [emception, setEmception] = useState<EmceptionWrapper>({
    worker: null,
  });
  const [emceptionLoaded, setEmceptionLoaded] = useState(false);

  const onprocessstart = (argv) => {
    // writeLineToConsole(`\$ ${argv.join(' ')}`);
    console.log(`\$ ${argv.join(' ')}`);
  };
  const onprocessend = () => {
    // writeLineToConsole(`Process finished`);
    console.log(`Process finished`);
  };

  async function loadEmception(): Promise<any> {
    if (emceptionLoaded) return;
    setEmceptionLoaded(true);

    // todo: is it possible to not refer as url?
    const emceptionWorker = new Worker(
      new URL('./emception.worker.ts', import.meta.url),
      { type: 'module' },
    );

    emceptionWorker.onerror = (e) => {
      console.error(e);
    };

    const emception: Comlink.Remote<Emception> = Comlink.wrap(emceptionWorker);

    setEmception({ worker: emception });

    emception.onstdout.bind(console.log);
    emception.onstderr.bind(console.log);
    emception.onprocessstart.bind(console.log);
    emception.onprocessend.bind(console.log);

    await emception.init();

    console.log('Post init');
    // showNotification('Ready');
  }

  useEffect(() => {
    if (emceptionLoaded) return;
    setEmceptionLoaded(true);
    loadEmception();
  }, []);

  return <div></div>;
}
