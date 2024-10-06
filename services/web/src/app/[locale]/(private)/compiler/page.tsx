'use client';

// import * as Comlink from 'comlink';
import Emception from './emception';
import { useEffect, useState } from 'react';

// type EmceptionWrapper = {
//   worker: Comlink.Remote<Emception> | null;
// };

export default function Page() {
  // const [emception, setEmception] = useState<EmceptionWrapper>({
  //   worker: null,
  // });
  const [emception, setEmception] = useState<Emception | null>(null);
  const [emceptionLoaded, setEmceptionLoaded] = useState(false);

  const onprocessstart = (argv) => {
    console.log(`\$ ${argv.join(' ')}`);
  };
  const onprocessend = () => {
    console.log(`Process finished`);
  };

  const loadEmception = async () => {
    if (emceptionLoaded) return;
    setEmceptionLoaded(true);

    // const emceptionWorker = new Worker(
    //   new URL('./emception.worker.js', import.meta.url),
    //   { type: 'module' },
    // );

    // emceptionWorker.onerror = (e) => {
    //   console.error(e);
    // };

    const emception = new Emception();

    // const emception: Comlink.Remote<Emception> = Comlink.wrap(emceptionWorker);

    // setEmception({ worker: emception });
    setEmception(emception);

    emception.onstdout.bind(console.log);
    emception.onstderr.bind(console.log);
    emception.onprocessstart.bind(console.log);
    emception.onprocessend.bind(console.log);

    console.log(emception.tools);

    console.log('pre init'); // prints
    await emception.init(); // something dies here but there is no error being thrown.
    console.log('Post init'); // this is not being printed
  };

  useEffect(() => {
    if (emceptionLoaded) return;
    setEmceptionLoaded(true);
    loadEmception();
  }, []);

  return <div></div>;
}
