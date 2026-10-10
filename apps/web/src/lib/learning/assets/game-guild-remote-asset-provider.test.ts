import { createAssetUri } from "@game-guild/assets";
import { afterEach, describe, expect, it, vi } from "vitest";
import { GameGuildRemoteAssetProvider } from "./game-guild-remote-asset-provider";

const scope = {
  type: "ProgramContent",
  id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
};
const assetId = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
const hash = "a".repeat(64);

function apiAsset() {
  return {
    id: assetId,
    displayName: "diagram.png",
    parentResourceType: scope.type,
    parentResourceId: scope.id,
    createdAt: "2026-09-12T12:00:00Z",
    updatedAt: "2026-09-12T12:01:00Z",
    content: {
      contentHash: hash,
      mimeType: "image/png",
      sizeBytes: 7,
      virusScanStatus: "Clean",
      moderationStatus: "Approved",
    },
  };
}

describe("GameGuildRemoteAssetProvider", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
  });

  it.each([
    "https://attacker.invalid/private",
    "//attacker.invalid/private",
    "\\\\attacker.invalid\\private",
    "../?next=https://attacker.invalid/#fragment",
    "%2f%2fattacker.invalid/private",
  ])(
    "keeps every asset request on the application origin for identifier %s",
    async (untrustedId) => {
      vi.stubEnv("NODE_ENV", "production");
      const origin = "https://gameguild.invalid";
      let firstLookup = true;
      const fetchMock = vi.fn(async (url: string, options?: RequestInit) => {
        if (firstLookup) {
          firstLookup = false;
          return new Response(null, { status: 404 });
        }
        if (options?.method === "POST") {
          return new Response(
            JSON.stringify({ assetReferenceId: untrustedId }),
            { status: 201 },
          );
        }
        if (options?.method === "DELETE")
          return new Response(null, { status: 204 });
        const requested = new URL(url, origin);
        if (requested.pathname.endsWith("/content"))
          return new Response("binary");
        if (requested.pathname === "/api/assets") {
          return new Response(JSON.stringify({ items: [] }));
        }
        return new Response(JSON.stringify(apiAsset()));
      });
      vi.stubGlobal("fetch", fetchMock);
      const provider = new GameGuildRemoteAssetProvider();
      const record = {
        id: untrustedId,
        uri: `asset://${untrustedId}`,
      } as Parameters<GameGuildRemoteAssetProvider["delete"]>[0];

      await provider.upload(
        [
          {
            id: untrustedId,
            uri: record.uri,
            blob: new Blob(["diagram"], { type: "image/png" }),
            name: "diagram.png",
            mimeType: "image/png",
          },
        ],
        { scope },
      );
      await provider.get(record.uri, { scope });
      await provider.list({ scope, search: untrustedId }, { scope });
      await provider.download(record, { scope });
      await provider.delete(record, { scope });

      expect(fetchMock).toHaveBeenCalledTimes(7);
      for (const [requested, options] of fetchMock.mock.calls) {
        expect(new URL(requested, origin).origin).toBe(origin);
        expect(options?.redirect).toBe("error");
      }
      const resolved = await provider.resolveUrl(record);
      expect(new URL(resolved.url).origin).toBe(window.location.origin);
    },
  );

  it("requires a complete learning scope", async () => {
    const provider = new GameGuildRemoteAssetProvider();
    await expect(provider.upload([], {})).rejects.toThrow(
      "A learning content scope is required for remote assets",
    );
    await expect(
      provider.list({}, { scope: { type: "", id: scope.id } }),
    ).rejects.toThrow("A learning content scope is required for remote assets");
    await expect(
      provider.list({}, { scope: { type: scope.type, id: "" } }),
    ).rejects.toThrow("A learning content scope is required for remote assets");
  });

  it("uploads into the learning-content scope while preserving the portable asset URI", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response(null, { status: 404 }))
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ assetReferenceId: assetId }), {
          status: 201,
        }),
      )
      .mockResolvedValueOnce(
        new Response(JSON.stringify(apiAsset()), { status: 200 }),
      );
    vi.stubGlobal("fetch", fetchMock);
    const provider = new GameGuildRemoteAssetProvider();
    const uri = createAssetUri(assetId);

    const [record] = await provider.upload(
      [
        {
          id: assetId,
          uri,
          blob: new Blob(["diagram"], { type: "image/png" }),
          name: "diagram.png",
          mimeType: "image/png",
        },
      ],
      { scope },
    );

    expect(record).toMatchObject({
      id: assetId,
      uri,
      scope,
      availability: "remote",
      contentHash: `sha256:${hash}`,
      location: {
        type: "provider",
        providerKey: "gameguild",
        providerAssetId: assetId,
      },
    });
    const uploadUrl = new URL(
      String(fetchMock.mock.calls[1]?.[0]),
      "http://localhost",
    );
    expect(uploadUrl.pathname).toBe("/api/assets");
    expect(uploadUrl.searchParams.get("referenceId")).toBe(assetId);
    expect(uploadUrl.searchParams.get("parentResourceType")).toBe(scope.type);
    expect(uploadUrl.searchParams.get("parentResourceId")).toBe(scope.id);
    expect(uploadUrl.searchParams.get("accessPolicy")).toBe("Private");
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({ method: "POST" });
  });

  it("lists scoped assets and resolves them through the authenticated content proxy", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          new Response(JSON.stringify({ items: [apiAsset()] }), {
            status: 200,
          }),
        ),
    );
    const provider = new GameGuildRemoteAssetProvider();

    const page = await provider.list({ scope }, { scope });
    const resolved = await provider.resolveUrl(page.items[0]!, { scope });

    expect(page.items).toHaveLength(1);
    expect(new URL(resolved.url).pathname).toBe(
      `/api/assets/${assetId}/content`,
    );
    expect(resolved.release()).toBeUndefined();
  });

  it("reuses an existing asset only inside the requested scope", async () => {
    const provider = new GameGuildRemoteAssetProvider();
    const input = {
      id: assetId,
      uri: createAssetUri(assetId),
      blob: new Blob(["diagram"], { type: "image/png" }),
      name: "diagram.png",
      mimeType: "image/png",
    };

    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValueOnce(
          new Response(JSON.stringify(apiAsset()), { status: 200 }),
        ),
    );
    await expect(provider.upload([input], { scope })).resolves.toHaveLength(1);

    for (const existing of [
      { ...apiAsset(), parentResourceType: "OtherResource" },
      {
        ...apiAsset(),
        parentResourceId: "cccccccc-cccc-4ccc-8ccc-cccccccccccc",
      },
    ]) {
      vi.stubGlobal(
        "fetch",
        vi
          .fn()
          .mockResolvedValueOnce(
            new Response(JSON.stringify(existing), { status: 200 }),
          ),
      );
      await expect(provider.upload([input], { scope })).rejects.toThrow(
        "The asset identifier belongs to another scope",
      );
    }
  });

  it("rejects failed lookups, uploads, changed identifiers, and missing uploaded assets", async () => {
    const provider = new GameGuildRemoteAssetProvider();
    const input = {
      id: assetId,
      uri: createAssetUri(assetId),
      blob: new Blob(["diagram"], { type: "image/png" }),
      name: "diagram.png",
      mimeType: "image/png",
    };

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValueOnce(new Response(null, { status: 401 })),
    );
    await expect(provider.upload([input], { scope })).rejects.toThrow(
      "The asset lookup failed",
    );

    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValueOnce(new Response(null, { status: 404 }))
        .mockResolvedValueOnce(new Response(null, { status: 500 })),
    );
    await expect(provider.upload([input], { scope })).rejects.toThrow(
      "The asset upload failed",
    );

    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValueOnce(new Response(null, { status: 404 }))
        .mockResolvedValueOnce(
          new Response(JSON.stringify({ assetReferenceId: "changed" }), {
            status: 201,
          }),
        ),
    );
    await expect(provider.upload([input], { scope })).rejects.toThrow(
      "The asset service changed the portable identifier",
    );

    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValueOnce(new Response(null, { status: 404 }))
        .mockResolvedValueOnce(
          new Response(JSON.stringify({ assetReferenceId: assetId }), {
            status: 201,
          }),
        )
        .mockResolvedValueOnce(new Response(null, { status: 404 })),
    );
    await expect(provider.upload([input], { scope })).rejects.toThrow(
      "The asset request failed",
    );
  });

  it("maps blocked, unnamed, unscoped, and corrupt API assets safely", async () => {
    const infected = {
      ...apiAsset(),
      displayName: "   ",
      parentResourceType: null,
      parentResourceId: scope.id,
      updatedAt: null,
      content: { ...apiAsset().content, virusScanStatus: "Infected" },
    };
    const moderated = {
      ...apiAsset(),
      displayName: null,
      parentResourceId: null,
      content: {
        ...apiAsset().content,
        virusScanStatus: "Clean",
        moderationStatus: "Blocked",
      },
    };
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          new Response(JSON.stringify({ items: [infected, moderated] }), {
            status: 200,
          }),
        ),
    );
    const provider = new GameGuildRemoteAssetProvider();
    const page = await provider.list(
      { search: "diagram", limit: 2 },
      { scope },
    );

    expect(page.items[0]).toMatchObject({
      name: assetId,
      availability: "unavailable",
      updatedAt: infected.createdAt,
    });
    expect(page.items[0]).not.toHaveProperty("scope");
    expect(page.items[1]).toMatchObject({ availability: "unavailable" });
    expect(page.items[1]).not.toHaveProperty("scope");
    const requestUrl = new URL(
      String(vi.mocked(fetch).mock.calls[0]?.[0]),
      "http://localhost",
    );
    expect(requestUrl.searchParams.get("search")).toBe("diagram");
    expect(requestUrl.searchParams.get("limit")).toBe("2");

    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValueOnce(
          new Response(JSON.stringify({ ...apiAsset(), content: null }), {
            status: 200,
          }),
        ),
    );
    await expect(
      provider.get(createAssetUri(assetId), { scope }),
    ).rejects.toThrow("Asset metadata has no content details");
  });

  it("gets, lists, and downloads through authenticated asset endpoints", async () => {
    const provider = new GameGuildRemoteAssetProvider();
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValueOnce(new Response(null, { status: 404 }))
        .mockResolvedValueOnce(
          new Response(JSON.stringify(apiAsset()), { status: 200 }),
        )
        .mockResolvedValueOnce(
          new Response(JSON.stringify({ items: [] }), { status: 200 }),
        )
        .mockResolvedValueOnce(new Response("binary", { status: 200 })),
    );

    await expect(
      provider.get(createAssetUri("dddddddd-dddd-4ddd-8ddd-dddddddddddd"), {
        scope,
      }),
    ).resolves.toBeNull();
    const record = await provider.get(createAssetUri(assetId), { scope });
    expect(record?.id).toBe(assetId);
    await expect(provider.list({ scope }, {})).resolves.toEqual({ items: [] });
    const download = await provider.download(record!, { scope });
    await expect(download.blob.text()).resolves.toBe("binary");
    expect(download.record).toBe(record);

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValueOnce(new Response(null, { status: 403 })),
    );
    await expect(provider.download(record!, { scope })).rejects.toThrow(
      "The asset download failed",
    );
  });

  it("surfaces failed reads and library requests", async () => {
    const provider = new GameGuildRemoteAssetProvider();
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValueOnce(new Response(null, { status: 500 })),
    );
    await expect(
      provider.get(createAssetUri(assetId), { scope }),
    ).rejects.toThrow("The asset request failed");

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValueOnce(new Response(null, { status: 404 })),
    );
    await expect(provider.list({ scope }, {})).rejects.toThrow(
      "The asset library request failed",
    );
  });

  it("resolves server-side URLs and treats deletion as idempotent", async () => {
    const provider = new GameGuildRemoteAssetProvider();
    const record = {
      id: assetId,
      uri: createAssetUri(assetId),
    } as Parameters<GameGuildRemoteAssetProvider["delete"]>[0];

    vi.stubGlobal("window", undefined);
    await expect(provider.resolveUrl(record)).resolves.toMatchObject({
      url: `http://localhost/api/assets/${assetId}/content`,
    });
    vi.unstubAllGlobals();

    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValueOnce(new Response(null, { status: 204 }))
        .mockResolvedValueOnce(new Response(null, { status: 404 })),
    );
    await expect(provider.delete(record, { scope })).resolves.toBeUndefined();
    await expect(provider.delete(record, { scope })).resolves.toBeUndefined();

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValueOnce(new Response(null, { status: 500 })),
    );
    await expect(provider.delete(record, { scope })).rejects.toThrow(
      "The asset delete failed",
    );
  });
});
