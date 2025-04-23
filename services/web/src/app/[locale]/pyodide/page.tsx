'use client';

import PyodideCodeInterface from '@/app/[locale]/pyodide/pyodide-code-interface';

export default function Index() {
  return (
    <>
      <div className="flex flex-auto sm:container max-w-lg">
        <PyodideCodeInterface
          initialCode={`name = "Twiggy"
leaves = 0
for day in range(3):
    leaves = leaves + 1
if leaves == 3:
    print(name, "has sprouted!")
`}
        />
      </div>
    </>
  );
}
