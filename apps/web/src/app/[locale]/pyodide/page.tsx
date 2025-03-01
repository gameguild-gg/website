'use client';


import { usePyodide } from '@/app/[locale]/pyodide/use-pyodite';
import CodeInterface from '@/app/[locale]/pyodide/code-interface';

export default function Index() {
  const { pyodideLoaded, loading, error, runPython, output } = usePyodide();

  return (
    <>
      <div className="flex flex-auto sm:container max-w-lg">
        <CodeInterface initialCode={`name = "Twiggy"
leaves = 0
for day in range(3):
    leaves = leaves + 1
if leaves == 3:
    print(name, "has sprouted!")
`} />
      </div>
    </>
  );
}
