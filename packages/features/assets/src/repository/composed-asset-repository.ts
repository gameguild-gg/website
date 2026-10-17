import type {
  AssetImportBlobOptions,
  AssetImportExternalOptions,
  AssetImportOptions,
  AssetPage,
  AssetPersistenceResult,
  AssetPortabilityReport,
  AssetQuery,
  AssetReadOptions,
  AssetReadTextOptions,
  AssetRecord,
  AssetRemoveOptions,
  AssetScope,
  AssetStorageStatus,
  AssetUsageInput,
  ResolvedAssetUrl,
} from "../core/asset-contracts";
import { AssetError } from "../core/asset-errors";
import { parseAssetUri, type AssetUri } from "../core/asset-uri";
import type { BrowserAssetRepository } from "../browser/browser-asset-repository";
import { hashBlob } from "../browser/content-hashing";
import type {
  AssetDownload,
  AssetProviderContext,
  RemoteAssetProvider,
} from "../providers/remote-asset-provider";
import { RemoteAssetProviderRegistry } from "../providers/remote-provider-registry";
import type { AssetRepository } from "./asset-repository";

export interface ComposedAssetRepositoryOptions {
  context?: (providerKey: string, signal?: AbortSignal) => AssetProviderContext;
}

function isMissingBytes(error: unknown): boolean {
  return error instanceof AssetError && error.code === "missing";
}

const CONTENT_HASH_PATTERN = /^sha256:[0-9a-f]{64}$/;

export class ComposedAssetRepository implements AssetRepository {
  constructor(
    private readonly browser: BrowserAssetRepository,
    private readonly providers: RemoteAssetProviderRegistry,
    private readonly options: ComposedAssetRepositoryOptions = {},
  ) {}

  private context(providerKey: string, signal?: AbortSignal): AssetProviderContext {
    return this.options.context?.(providerKey, signal) ?? { signal };
  }

  private providerFor(record: AssetRecord): RemoteAssetProvider {
    const provider = this.providers.forRecord(record);
    if (!provider) {
      const key = record.location.type === "local" ? "local" : record.location.providerKey;
      throw new AssetError("missing", `No provider is registered for asset location: ${key}`);
    }
    return provider;
  }

  private validateProviderRecord(
    record: AssetRecord,
    provider: RemoteAssetProvider,
    expectedUri?: AssetUri,
  ): AssetRecord {
    const identity = parseAssetUri(record.uri);
    if (
      !identity ||
      identity.id !== record.id ||
      record.location.type === "local" ||
      record.location.providerKey !== provider.key ||
      !record.name.trim() ||
      !record.mimeType.trim() ||
      !Number.isSafeInteger(record.size) ||
      record.size < 0 ||
      (record.contentHash !== undefined && !CONTENT_HASH_PATTERN.test(record.contentHash)) ||
      (record.location.type === "external" && !record.location.reference.trim()) ||
      (record.location.type === "provider" && record.location.providerAssetId === "") ||
      (expectedUri && record.uri !== expectedUri)
    ) {
      throw new AssetError("corrupt", `Provider ${provider.key} returned an invalid asset record`);
    }
    return record;
  }

  private async lookup(uri: AssetUri): Promise<AssetRecord | null> {
    for (const provider of this.providers.list()) {
      if (!provider.capabilities.lookup) continue;
      const record = await provider.get(uri, this.context(provider.key));
      if (!record) continue;
      const valid = this.validateProviderRecord(record, provider, uri);
      await this.browser.storeMetadata(valid);
      return valid;
    }
    return null;
  }

  private async downloadRemote(
    record: AssetRecord,
    signal?: AbortSignal,
  ): Promise<AssetDownload> {
    const provider = this.providerFor(record);
    if (!provider.capabilities.download) {
      throw new AssetError("unsupported", `Provider ${provider.key} cannot download assets`);
    }
    const download = await provider.download(record, this.context(provider.key, signal));
    this.validateProviderRecord(download.record, provider, record.uri);
    if (download.blob.size !== download.record.size) {
      throw new AssetError("corrupt", `Provider ${provider.key} returned an invalid asset size`);
    }
    if (download.record.contentHash) {
      const actualHash = await hashBlob(download.blob, signal);
      if (actualHash !== download.record.contentHash) {
        throw new AssetError("corrupt", `Provider ${provider.key} returned invalid asset content`);
      }
    }
    return {
      ...download,
      blob: download.blob.type === download.record.mimeType
        ? download.blob
        : download.blob.slice(0, download.blob.size, download.record.mimeType),
    };
  }

  importFiles(files: readonly File[], options?: AssetImportOptions): Promise<AssetRecord[]> {
    return this.browser.importFiles(files, options);
  }

  importBlob(blob: Blob, options: AssetImportBlobOptions): Promise<AssetRecord> {
    return this.browser.importBlob(blob, options);
  }

  importExternal(options: AssetImportExternalOptions): Promise<AssetRecord> {
    return this.browser.importExternal(options);
  }

  async get(uri: AssetUri): Promise<AssetRecord | null> {
    return (await this.browser.get(uri)) ?? this.lookup(uri);
  }

  async list(query: AssetQuery = {}): Promise<AssetPage> {
    const local = await this.browser.list(query);
    if (!query.includeRemote) return local;
    const providerRecords = await Promise.all(
      this.providers.list()
        .filter((provider) => provider.capabilities.list)
        .map(async (provider) => {
          const page = await provider.list(query, this.context(provider.key));
          return Promise.all(page.items.map(async (record) => {
            const valid = this.validateProviderRecord(record, provider);
            await this.browser.storeMetadata(valid);
            return valid;
          }));
        }),
    );
    const unique = new Map<AssetUri, AssetRecord>();
    for (const record of [...local.items, ...providerRecords.flat()]) unique.set(record.uri, record);
    return { items: Array.from(unique.values()) };
  }

  async listUsedByScope(scope: AssetScope): Promise<AssetRecord[]> {
    const records = await Promise.all(
      (await this.browser.listUsageUrisByScope(scope)).map((uri) => this.get(uri)),
    );
    return records.filter((record): record is AssetRecord => Boolean(record));
  }

  async checkPortability(uris: readonly AssetUri[]): Promise<AssetPortabilityReport> {
    const localOnly: AssetUri[] = [];
    const unavailable: AssetUri[] = [];
    for (const uri of new Set(uris)) {
      try {
        const record = await this.get(uri);
        if (!record || record.availability === "unavailable" || record.availability === "failed") {
          unavailable.push(uri);
        } else if (record.location.type === "local") {
          localOnly.push(uri);
        } else if (!this.providers.forRecord(record)) {
          unavailable.push(uri);
        }
      } catch {
        unavailable.push(uri);
      }
    }
    return { portable: localOnly.length === 0 && unavailable.length === 0, localOnly, unavailable };
  }

  async uploadLocalAsset(
    localUri: AssetUri,
    providerKey: string,
    options: { signal?: AbortSignal; scope?: AssetScope } = {},
  ): Promise<AssetRecord> {
    const provider = this.providers.get(providerKey);
    if (!provider || !provider.capabilities.upload) {
      throw new AssetError("unsupported", `Provider ${providerKey} cannot upload assets`);
    }
    const record = await this.browser.get(localUri);
    if (!record) throw new AssetError("missing", `Asset not found: ${localUri}`);
    if (record.location.type !== "local") {
      throw new AssetError("invalid", "Only browser-local assets can be uploaded");
    }
    const blob = await this.browser.readBlob(localUri, { signal: options.signal });
    const [remote] = await provider.upload(
      [{ id: record.id, uri: record.uri, blob, name: record.name, mimeType: record.mimeType }],
      { ...this.context(providerKey, options.signal), scope: options.scope ?? record.scope },
    );
    if (!remote) {
      throw new AssetError("corrupt", `Provider ${providerKey} did not return an uploaded asset`);
    }
    const valid = this.validateProviderRecord(remote, provider, localUri);
    if (
      valid.size !== record.size ||
      (valid.contentHash !== undefined && valid.contentHash !== record.contentHash)
    ) {
      throw new AssetError("corrupt", `Provider ${providerKey} changed uploaded asset content`);
    }
    const promoted: AssetRecord = {
      ...valid,
      contentHash: valid.contentHash ?? record.contentHash,
      createdAt: record.createdAt,
    };
    await this.browser.storeMetadata(promoted);
    return promoted;
  }

  async readBlob(uri: AssetUri, options: AssetReadOptions = {}): Promise<Blob> {
    try {
      return await this.browser.readBlob(uri, options);
    } catch (error) {
      if (!isMissingBytes(error)) throw error;
    }
    const record = await this.get(uri);
    if (!record) throw new AssetError("missing", `Asset not found: ${uri}`);
    if (record.location.type === "local") {
      throw new AssetError("missing", `Asset bytes are not stored in this browser: ${uri}`);
    }
    return (await this.downloadRemote(record, options.signal)).blob;
  }

  async readStream(
    uri: AssetUri,
    options: AssetReadOptions = {},
  ): Promise<ReadableStream<Uint8Array>> {
    return (await this.readBlob(uri, options)).stream();
  }

  async readText(uri: AssetUri, options: AssetReadTextOptions = {}): Promise<string> {
    const blob = await this.readBlob(uri, options);
    return !options.encoding || options.encoding.toLowerCase() === "utf-8"
      ? blob.text()
      : new TextDecoder(options.encoding).decode(await blob.arrayBuffer());
  }

  async createObjectUrl(uri: AssetUri): Promise<ResolvedAssetUrl> {
    try {
      return await this.browser.createObjectUrl(uri);
    } catch (error) {
      if (!isMissingBytes(error)) throw error;
    }
    const record = await this.get(uri);
    if (!record) throw new AssetError("missing", `Asset not found: ${uri}`);
    const provider = this.providerFor(record);
    if (!provider.capabilities.resolveUrl) {
      throw new AssetError("unsupported", `Provider ${provider.key} cannot resolve asset URLs`);
    }
    const resolved = await provider.resolveUrl(record, this.context(provider.key));
    let protocol: string;
    try {
      protocol = new URL(resolved.url).protocol;
    } catch {
      resolved.release();
      throw new AssetError("corrupt", `Provider ${provider.key} returned an invalid asset URL`);
    }
    if (protocol !== "https:" && protocol !== "http:" && protocol !== "blob:") {
      resolved.release();
      throw new AssetError("corrupt", `Provider ${provider.key} returned an unsafe asset URL`);
    }
    return resolved;
  }

  async rename(uri: AssetUri, name: string): Promise<AssetRecord> {
    const record = await this.get(uri);
    if (!record) throw new AssetError("missing", `Asset not found: ${uri}`);
    if (record.location.type === "provider") {
      const provider = this.providerFor(record);
      if (!provider.rename) {
        throw new AssetError("unsupported", `Provider ${provider.key} does not support remote rename`);
      }
      return provider.rename(record.id, name, this.context(provider.key));
    }
    return this.browser.rename(uri, name);
  }

  async remove(uri: AssetUri, options: AssetRemoveOptions = {}): Promise<void> {
    const record = await this.get(uri);
    if (!record) throw new AssetError("missing", `Asset not found: ${uri}`);
    if (record.location.type === "provider") {
      if ((await this.browser.hasUsage(uri)) && !options.force) {
        throw new AssetError("invalid", "Asset is still referenced");
      }
      const provider = this.providerFor(record);
      if (!provider.delete || !provider.capabilities.delete) {
        throw new AssetError("unsupported", `Provider ${provider.key} cannot delete assets`);
      }
      await provider.delete(record, this.context(provider.key, options.signal));
    }
    await this.browser.remove(uri, options);
  }

  reconcileUsage(scope: AssetScope, usages: readonly AssetUsageInput[]): Promise<void> {
    return this.browser.reconcileUsage(scope, usages);
  }

  getStorageStatus(): Promise<AssetStorageStatus> {
    return this.browser.getStorageStatus();
  }

  requestPersistentStorage(): Promise<AssetPersistenceResult> {
    return this.browser.requestPersistentStorage();
  }
}
