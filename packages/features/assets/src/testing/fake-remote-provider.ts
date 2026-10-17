import type { AssetPage, AssetQuery, AssetRecord } from "../core/asset-contracts";
import type { AssetUri } from "../core/asset-uri";
import { classifyAssetKind, inferMimeType } from "../core/mime";
import { createAssetUri } from "../core/asset-uri";
import { hashBlob } from "../browser/content-hashing";
import type {
  AssetDownload,
  AssetProviderContext,
  AssetUploadInput,
  RemoteAssetProvider,
} from "../providers/remote-asset-provider";

export class FakeRemoteAssetProvider implements RemoteAssetProvider {
  readonly key = "fake";
  readonly capabilities = {
    upload: true,
    lookup: true,
    list: true,
    download: true,
    resolveUrl: true,
    delete: true,
  } as const;

  private readonly values = new Map<AssetUri, AssetDownload>();
  downloadCalls = 0;
  resolveUrlCalls = 0;

  async upload(
    files: readonly AssetUploadInput[],
    context: AssetProviderContext,
  ): Promise<AssetRecord[]> {
    if (context.signal?.aborted) throw new DOMException("Aborted", "AbortError");
    return Promise.all(files.map(async (file) => {
      const now = new Date().toISOString();
      const mimeType = inferMimeType(file.name, file.mimeType || file.blob.type);
      const record: AssetRecord = {
        id: file.id,
        uri: file.uri,
        name: file.name,
        kind: classifyAssetKind(mimeType, file.name),
        mimeType,
        size: file.blob.size,
        contentHash: await hashBlob(file.blob, context.signal),
        location: { type: "provider", providerKey: this.key, providerAssetId: file.id },
        availability: "remote",
        createdAt: now,
        updatedAt: now,
        source: { type: "remote", value: this.key },
        scope: context.scope,
      };
      this.values.set(file.uri, { blob: file.blob, record });
      return structuredClone(record);
    }));
  }

  async get(uri: AssetUri): Promise<AssetRecord | null> {
    return this.values.get(uri)?.record ?? null;
  }

  async list(query: AssetQuery): Promise<AssetPage> {
    const search = query.search?.trim().toLocaleLowerCase();
    const items = Array.from(this.values.values(), ({ record }) => record).filter((record) => {
      if (search && !record.name.toLocaleLowerCase().includes(search)) return false;
      if (query.kinds?.length && !query.kinds.includes(record.kind)) return false;
      if (query.scope &&
        (record.scope?.type !== query.scope.type || record.scope.id !== query.scope.id)) return false;
      return true;
    });
    return { items: structuredClone(items) };
  }

  async download(record: AssetRecord): Promise<AssetDownload> {
    this.downloadCalls += 1;
    const value = this.values.get(record.uri);
    if (!value) throw new Error(`Remote asset not found: ${record.uri}`);
    return value;
  }

  async resolveUrl(record: AssetRecord) {
    this.resolveUrlCalls += 1;
    const value = this.values.get(record.uri);
    if (!value) throw new Error(`Remote asset not found: ${record.uri}`);
    const url = URL.createObjectURL(value.blob);
    let released = false;
    return {
      url,
      release: () => {
        if (released) return;
        released = true;
        URL.revokeObjectURL(url);
      },
    };
  }

  async delete(record: AssetRecord): Promise<void> {
    this.values.delete(record.uri);
  }

  async rename(id: string, displayName: string, context: AssetProviderContext): Promise<AssetRecord> {
    if (context.signal?.aborted) throw new DOMException("Aborted", "AbortError");
    const uri = createAssetUri(id);
    const value = this.values.get(uri);
    if (!value) throw new Error(`Remote asset not found: ${uri}`);

    value.record.name = displayName;
    return structuredClone(value.record);
  }

  seed(uri: AssetUri, download: AssetDownload): void {
    this.values.set(uri, download);
  }
}
