import { isShikiTheme, type ShikiTheme } from "../monaco/shiki-themes";

export type EditorModalSize =
  "compact" | "widescreen" | "ultrawide" | "fullscreen";

export type EditorRenderWhitespace = "none" | "boundary" | "all";
export type EditorLineHighlight = "none" | "gutter" | "line" | "all";

export interface MonacoSurfacePreferences {
  shikiTheme: ShikiTheme;
  fontSize: number;
  lineNumbers: boolean;
  wordWrap: boolean;
  minimap: boolean;
  tabSize: number;
  renderWhitespace: EditorRenderWhitespace;
  renderLineHighlight: EditorLineHighlight;
}

export interface FeatureEditorPreferences {
  modalSize: EditorModalSize;
  editor: MonacoSurfacePreferences;
}

interface PersistedPreferences {
  global?: Partial<FeatureEditorPreferences> & {
    editor?: Partial<MonacoSurfacePreferences>;
  };
  nodeTypes?: Record<string, Partial<FeatureEditorPreferences>>;
}

const DB_NAME = "editor-preferences";
const DB_VERSION = 1;
const STORE_NAME = "preferences";
const PREFERENCES_KEY = "editor-prefs";

export const DEFAULT_MONACO_PREFERENCES: MonacoSurfacePreferences = {
  shikiTheme: "github",
  fontSize: 14,
  lineNumbers: true,
  wordWrap: true,
  minimap: false,
  tabSize: 2,
  renderWhitespace: "none",
  renderLineHighlight: "line",
};

export const DEFAULT_FEATURE_EDITOR_PREFERENCES: FeatureEditorPreferences = {
  modalSize: "widescreen",
  editor: DEFAULT_MONACO_PREFERENCES,
};

export function normalizeMonacoPreferences(
  preferences: Omit<
    Partial<MonacoSurfacePreferences>,
    "renderLineHighlight"
  > & {
    renderLineHighlight?: EditorLineHighlight | "rectangle";
  },
): MonacoSurfacePreferences {
  const renderLineHighlight = preferences.renderLineHighlight ?? "line";

  return {
    shikiTheme: isShikiTheme(preferences.shikiTheme)
      ? preferences.shikiTheme
      : "github",
    fontSize: Math.max(10, Math.min(24, Number(preferences.fontSize) || 14)),
    lineNumbers: preferences.lineNumbers !== false,
    wordWrap: preferences.wordWrap !== false,
    minimap: preferences.minimap === true,
    tabSize: [2, 4, 8].includes(Number(preferences.tabSize))
      ? Number(preferences.tabSize)
      : 2,
    renderWhitespace: ["none", "boundary", "all"].includes(
      preferences.renderWhitespace ?? "none",
    )
      ? (preferences.renderWhitespace ?? "none")
      : "none",
    renderLineHighlight: ["none", "gutter", "line", "all"].includes(
      renderLineHighlight,
    )
      ? (renderLineHighlight as EditorLineHighlight)
      : "line",
  };
}

const listeners = new Set<() => void>();

function openPreferencesDatabase(): Promise<IDBDatabase | null> {
  if (typeof indexedDB === "undefined") return Promise.resolve(null);

  return new Promise((resolve) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);
    request.onerror = () => resolve(null);
    request.onsuccess = () => { resolve(request.result); };
    request.onupgradeneeded = () => {
      const database = request.result;
      if (!database.objectStoreNames.contains(STORE_NAME)) {
        database.createObjectStore(STORE_NAME);
      }
    };
  });
}

async function readPersistedPreferences(): Promise<PersistedPreferences> {
  const database = await openPreferencesDatabase();
  if (!database) return {};

  return new Promise((resolve) => {
    const request = database
      .transaction(STORE_NAME, "readonly")
      .objectStore(STORE_NAME)
      .get(PREFERENCES_KEY);
    request.onerror = () => resolve({});
    request.onsuccess = () => resolve(request.result ?? {});
  });
}

async function writePersistedPreferences(
  preferences: PersistedPreferences,
): Promise<void> {
  const database = await openPreferencesDatabase();
  if (!database) return;

  await new Promise<void>((resolve) => {
    const request = database
      .transaction(STORE_NAME, "readwrite")
      .objectStore(STORE_NAME)
      .put(preferences, PREFERENCES_KEY);
    request.onerror = () => resolve();
    request.onsuccess = () => resolve();
  });
}

export async function getFeatureEditorPreferences(
  feature: string,
): Promise<FeatureEditorPreferences> {
  const persisted = await readPersistedPreferences();
  const global = persisted.global ?? {};
  const featurePreferences = persisted.nodeTypes?.[feature] ?? {};

  const editor = {
    ...DEFAULT_MONACO_PREFERENCES,
    ...global.editor,
    ...featurePreferences.editor,
  };

  return {
    modalSize:
      featurePreferences.modalSize ??
      global.modalSize ??
      DEFAULT_FEATURE_EDITOR_PREFERENCES.modalSize,
    editor: normalizeMonacoPreferences(editor),
  };
}

export async function setFeatureModalSize(
  feature: string,
  modalSize: EditorModalSize,
): Promise<void> {
  const persisted = await readPersistedPreferences();
  persisted.nodeTypes ??= {};
  persisted.nodeTypes[feature] = {
    ...persisted.nodeTypes[feature],
    modalSize,
  };
  await writePersistedPreferences(persisted);
  listeners.forEach((listener) => listener());
}

export async function setGlobalMonacoPreference<
  Key extends keyof MonacoSurfacePreferences,
>(key: Key, value: MonacoSurfacePreferences[Key]): Promise<void> {
  const persisted = await readPersistedPreferences();
  persisted.global ??= {};
  persisted.global.editor = {
    ...DEFAULT_MONACO_PREFERENCES,
    ...persisted.global.editor,
    [key]: value,
  };
  await writePersistedPreferences(persisted);
  listeners.forEach((listener) => listener());
}

export function subscribeToFeatureEditorPreferences(
  listener: () => void,
): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}
