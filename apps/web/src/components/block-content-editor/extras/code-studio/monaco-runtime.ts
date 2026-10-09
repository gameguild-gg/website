type MonacoApi = typeof import('monaco-editor');
type TypeScriptSupport = typeof import('monaco-editor').typescript;

// Monaco 0.56 exposes TypeScript at the top level; older browser loaders expose
// it under languages. Use the published package exports instead of React's
// deep-import alias, which cannot resolve against Monaco 0.56's export map.
export type MonacoRuntime = Pick<MonacoApi, 'Range' | 'languages'> & {
  languages: MonacoApi['languages'] & { typescript?: TypeScriptSupport };
  typescript?: TypeScriptSupport;
};

export function getTypeScriptSupport(monaco: Pick<MonacoRuntime, 'languages' | 'typescript'> | null): TypeScriptSupport | undefined {
  return monaco?.typescript ?? monaco?.languages.typescript;
}
