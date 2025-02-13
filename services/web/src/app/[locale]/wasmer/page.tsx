'use client';

import CodeInterface from '@/app/[locale]/wasmer/code-interface';

export default function Page() {
  return (
    <div className="flex flex-auto flex-col items-center justify-center min-h-screen bg-gray-900 text-white p-6">
      <h1 className="text-2xl font-bold mb-4">Code Embedding PoC</h1>
      <p className="mb-6 text-gray-400">Proof of Concept for executing code on browser for teaching purposes.</p>

      <CodeInterface data='print("Hello, World!")' language="python" />
    </div>
  );
}
