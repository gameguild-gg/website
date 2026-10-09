import {
  AssetError,
  classifyAssetKind,
  createAssetUri,
  type AssetPage,
  type AssetQuery,
  type AssetRecord,
  type AssetScope,
} from "@game-guild/assets";
import type {
  AssetDownload,
  AssetProviderContext,
  AssetUploadInput,
  RemoteAssetProvider,
} from "@game-guild/assets/providers";
import { assertSafeRemoteUrl } from "@/lib/security/safe-remote-url";

interface ApiAssetContent {
  contentHash: string;
  mimeType: string;
  sizeBytes: number;
  virusScanStatus?: string;
  moderationStatus?: string;
}

interface ApiAsset {
  id: string;
  displayName?: string | null;
  parentResourceType?: string | null;
  parentResourceId?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  content?: ApiAssetContent | null;
}

function requiredScope(context: AssetProviderContext): AssetScope {
  if (!context.scope?.type || !context.scope.id) {
    throw new AssetError("invalid", "A learning content scope is required for remote assets");
  }
  return context.scope;
}

function apiError(status: number, fallback: string): AssetError {
  if (status === 401 || status === 403) return new AssetError("invalid", fallback);
  if (status === 404) return new AssetError("missing", fallback);
  return new AssetError("storage-unavailable", fallback);
}

function toRecord(asset: ApiAsset): AssetRecord {
  if (!asset.content) throw new AssetError("corrupt", "Asset metadata has no content details");
  const blocked = asset.content.virusScanStatus === "Infected" ||
    asset.content.moderationStatus === "Blocked";
  const scope = asset.parentResourceType && asset.parentResourceId
    ? { type: asset.parentResourceType, id: asset.parentResourceId }
    : undefined;
  return {
    id: asset.id,
    uri: createAssetUri(asset.id),
    name: asset.displayName?.trim() || asset.id,
    kind: classifyAssetKind(asset.content.mimeType, asset.displayName ?? ""),
    mimeType: asset.content.mimeType,
    size: asset.content.sizeBytes,
    contentHash: `sha256:${asset.content.contentHash.toLowerCase()}`,
    location: { type: "provider", providerKey: "gameguild", providerAssetId: asset.id },
    availability: blocked ? "unavailable" : "remote",
    createdAt: asset.createdAt,
    updatedAt: asset.updatedAt ?? asset.createdAt,
    source: { type: "remote", value: "gameguild" },
    ...(scope ? { scope } : {}),
  };
}

async function readAsset(response: Response): Promise<AssetRecord> {
  if (!response.ok) throw apiError(response.status, "The asset request failed");
  return toRecord(await response.json() as ApiAsset);
}

export class GameGuildRemoteAssetProvider implements RemoteAssetProvider {
  readonly key = "gameguild";
  readonly capabilities = {
    upload: true,
    lookup: true,
    list: true,
    download: true,
    resolveUrl: true,
    delete: true,
  } as const;

  async upload(
    files: readonly AssetUploadInput[],
    context: AssetProviderContext,
  ): Promise<AssetRecord[]> {
    const scope = requiredScope(context);
    const records: AssetRecord[] = [];
    for (const file of files) {
      const existing = await fetch(assertSafeRemoteUrl(`/api/assets/${encodeURIComponent(file.id)}?includeContent=true`), {
        redirect: "error",
        cache: "no-store",
        signal: context.signal,
      });
      if (existing.ok) {
        const record = await readAsset(existing);
        if (record.scope?.type !== scope.type || record.scope.id !== scope.id) {
          throw new AssetError("invalid", "The asset identifier belongs to another scope");
        }
        records.push(record);
        continue;
      }
      if (existing.status !== 404) throw apiError(existing.status, "The asset lookup failed");

      const query = new URLSearchParams({
        referenceId: file.id,
        displayName: file.name,
        accessPolicy: "Private",
        parentResourceType: scope.type,
        parentResourceId: scope.id,
      });
      const body = new FormData();
      body.set("file", file.blob, file.name);
      const uploaded = await fetch(assertSafeRemoteUrl(`/api/assets?${query}`), {
        redirect: "error",
        method: "POST",
        body,
        cache: "no-store",
        signal: context.signal,
      });
      if (!uploaded.ok) throw apiError(uploaded.status, "The asset upload failed");
      const result = await uploaded.json() as { assetReferenceId?: string };
      if (result.assetReferenceId !== file.id) {
        throw new AssetError("corrupt", "The asset service changed the portable identifier");
      }
      records.push(await readAsset(await fetch(assertSafeRemoteUrl(`/api/assets/${encodeURIComponent(file.id)}?includeContent=true`),
        { cache: "no-store", signal: context.signal, redirect: "error" },
      )));
    }
    return records;
  }

  async get(uri: AssetRecord["uri"], context: AssetProviderContext): Promise<AssetRecord | null> {
    const id = uri.slice("asset://".length);
    const response = await fetch(assertSafeRemoteUrl(`/api/assets/${encodeURIComponent(id)}?includeContent=true`), {
      redirect: "error",
      cache: "no-store",
      signal: context.signal,
    });
    if (response.status === 404) return null;
    return readAsset(response);
  }

  async list(query: AssetQuery, context: AssetProviderContext): Promise<AssetPage> {
    const scope = query.scope ?? requiredScope(context);
    const params = new URLSearchParams({ resourceType: scope.type, resourceId: scope.id });
    if (query.search) params.set("search", query.search);
    if (query.limit) params.set("limit", String(query.limit));
    const response = await fetch(assertSafeRemoteUrl(`/api/assets?${params}`), {
      redirect: "error",
      cache: "no-store",
      signal: context.signal,
    });
    if (!response.ok) throw apiError(response.status, "The asset library request failed");
    const payload = await response.json() as { items: ApiAsset[] };
    return { items: payload.items.map(toRecord) };
  }

  async download(record: AssetRecord, context: AssetProviderContext): Promise<AssetDownload> {
    const response = await fetch(assertSafeRemoteUrl(this.contentUrl(record.id)), {
      redirect: "error",
      cache: "no-store",
      signal: context.signal,
    });
    if (!response.ok) throw apiError(response.status, "The asset download failed");
    return { blob: await response.blob(), record };
  }

  async resolveUrl(record: AssetRecord): Promise<{ url: string; release: () => void }> {
    const base = typeof window === "undefined" ? "http://localhost" : window.location.origin;
    return { url: new URL(this.contentUrl(record.id), base).toString(), release: () => undefined };
  }

  async delete(record: AssetRecord, context: AssetProviderContext): Promise<void> {
    const response = await fetch(assertSafeRemoteUrl(`/api/assets/${encodeURIComponent(record.id)}`), {
      redirect: "error",
      method: "DELETE",
      cache: "no-store",
      signal: context.signal,
    });
    if (!response.ok && response.status !== 404) throw apiError(response.status, "The asset delete failed");
  }

  private contentUrl(id: string): string {
    return `/api/assets/${encodeURIComponent(id)}/content`;
  }
}
