"use client";

type MonacoWorkerEnvironment = {
  getWorker?: (workerId: string, label: string) => Worker | Promise<Worker>;
  getWorkerUrl?: (workerId: string, label: string) => string;
};

type MonacoWorkerGlobal = typeof globalThis & {
  MonacoEnvironment?: MonacoWorkerEnvironment;
};

let configured = false;

/**
 * Monaco's default ESM bootstrap imports a root-relative Next asset from a
 * blob URL, which Chromium cannot resolve in production builds. Supplying
 * workers directly avoids that bootstrap and keeps language services local.
 */
export function configureMonacoWorkers() {
  if (configured || typeof window === "undefined") return;

  const scope = globalThis as MonacoWorkerGlobal;
  scope.MonacoEnvironment = {
    ...scope.MonacoEnvironment,
    getWorker(_workerId, label) {
      if (label === "json") {
        return new Worker(
          new URL(
            "monaco-editor/language/json/json.worker.js",
            import.meta.url,
          ),
          { name: label, type: "module" },
        );
      }

      if (label === "css" || label === "scss" || label === "less") {
        return new Worker(
          new URL(
            "monaco-editor/language/css/css.worker.js",
            import.meta.url,
          ),
          { name: label, type: "module" },
        );
      }

      if (label === "html" || label === "handlebars" || label === "razor") {
        return new Worker(
          new URL(
            "monaco-editor/language/html/html.worker.js",
            import.meta.url,
          ),
          { name: label, type: "module" },
        );
      }

      if (label === "typescript" || label === "javascript") {
        return new Worker(
          new URL(
            "monaco-editor/language/typescript/ts.worker.js",
            import.meta.url,
          ),
          { name: label, type: "module" },
        );
      }

      return new Worker(
        new URL("monaco-editor/editor/editor.worker.js", import.meta.url),
        { name: label, type: "module" },
      );
    },
  };
  configured = true;
}
