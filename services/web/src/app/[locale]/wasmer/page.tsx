'use client';

import CodeInterface from '@/app/[locale]/wasmer/code-interface';

export default function Page() {
  return (
    <div className="flex flex-auto flex-col items-center justify-center min-h-screen bg-gray-900 text-white p-6">
      <h1 className="text-2xl font-bold mb-4">Wasmer Code Executor</h1>
      <p className="mb-6 text-gray-400">Execute código em Wasmer diretamente no navegador.</p>

      <CodeInterface initialCode="print('Hello, Wasmer!')" />
    </div>
  );
}
