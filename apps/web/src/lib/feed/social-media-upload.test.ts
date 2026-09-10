import { describe, expect, it, vi } from "vitest";

import { uploadSocialMediaWithProgress } from "./social-media-upload";

class FakeUpload {
  onprogress: ((event: ProgressEvent<EventTarget>) => void) | null = null;
}

class FakeXhr {
  static instance: FakeXhr | null = null;
  static completeImmediately = true;
  upload = new FakeUpload();
  status = 201;
  responseText = JSON.stringify({ assetReferenceId: "asset-1", state: "Ready", sizeBytes: 5 });
  onload: (() => void) | null = null;
  onerror: (() => void) | null = null;
  onabort: (() => void) | null = null;
  open = vi.fn();
  send = vi.fn(() => {
    if (!FakeXhr.completeImmediately) return;
    this.upload.onprogress?.({ lengthComputable: true, loaded: 5, total: 10 } as ProgressEvent<EventTarget>);
    this.onload?.();
  });
  abort = vi.fn(() => this.onabort?.());
  constructor() { FakeXhr.instance = this; }
}

describe("uploadSocialMediaWithProgress", () => {
  it("uses the same-origin upload route and reports uploaded bytes", async () => {
    vi.stubGlobal("XMLHttpRequest", FakeXhr);
    const onProgress = vi.fn();
    const file = new File(["hello"], "build.png", { type: "image/png" });

    await expect(uploadSocialMediaWithProgress(file, { onProgress })).resolves.toMatchObject({ assetReferenceId: "asset-1", state: "Ready" });
    expect(FakeXhr.instance?.open).toHaveBeenCalledWith("POST", "/api/social/media");
    expect(onProgress).toHaveBeenCalledWith({ loaded: 5, total: 10, percent: 50 });
    vi.unstubAllGlobals();
  });

  it("aborts the browser transport when cancelled", async () => {
    vi.stubGlobal("XMLHttpRequest", FakeXhr);
    FakeXhr.completeImmediately = false;
    const controller = new AbortController();
    const upload = uploadSocialMediaWithProgress(new File(["hello"], "build.png", { type: "image/png" }), { signal: controller.signal });
    controller.abort();
    await expect(upload).rejects.toThrow(/cancelled/i);
    expect(FakeXhr.instance?.abort).toHaveBeenCalledTimes(1);
    FakeXhr.completeImmediately = true;
    vi.unstubAllGlobals();
  });
});
