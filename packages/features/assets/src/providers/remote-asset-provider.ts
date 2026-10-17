import type {
  AssetPage,
  AssetQuery,
  AssetRecord,
  AssetScope,
  ResolvedAssetUrl,
} from "../core/asset-contracts";
import type { AssetUri } from "../core/asset-uri";

export interface RemoteAssetProviderCapabilities {
  upload: boolean;
  lookup: boolean;
  list: boolean;
  download: boolean;
  resolveUrl: boolean;
  delete?: boolean;
  revisions?: boolean;
  transforms?: boolean;
}

export interface AssetProviderContext {
  scope?: AssetScope;
  signal?: AbortSignal;
  host?: unknown;
}

export interface AssetUploadInput {
  id: string;
  uri: AssetUri;
  blob: Blob;
  name: string;
  mimeType: string;
}

export interface AssetDownload {
  blob: Blob;
  record: AssetRecord;
}

export interface RemoteAssetProvider {
  readonly key: string;
  readonly capabilities: RemoteAssetProviderCapabilities;
  upload(
    files: readonly AssetUploadInput[],
    context: AssetProviderContext,
  ): Promise<AssetRecord[]>;
  get(uri: AssetUri, context: AssetProviderContext): Promise<AssetRecord | null>;
  list(query: AssetQuery, context: AssetProviderContext): Promise<AssetPage>;
  download(record: AssetRecord, context: AssetProviderContext): Promise<AssetDownload>;
  resolveUrl(record: AssetRecord, context: AssetProviderContext): Promise<ResolvedAssetUrl>;
  delete?(record: AssetRecord, context: AssetProviderContext): Promise<void>;
  rename?(id: string, displayName: string, context: AssetProviderContext): Promise<AssetRecord>;
}
