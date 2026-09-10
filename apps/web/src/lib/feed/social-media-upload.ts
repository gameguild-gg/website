"use client";

import type { SocialMediaAsset } from "./contracts";

export interface UploadProgress {
  loaded: number;
  total: number;
  percent: number;
}

export class SocialMediaUploadError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "SocialMediaUploadError";
  }
}

export function uploadSocialMediaWithProgress(
  file: File,
  options: { signal?: AbortSignal; onProgress?: (progress: UploadProgress) => void } = {},
): Promise<SocialMediaAsset> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    let settled = false;
    const finish = (callback: () => void) => {
      if (settled) return;
      settled = true;
      options.signal?.removeEventListener("abort", abort);
      callback();
    };
    const abort = () => {
      xhr.abort();
      finish(() => reject(new SocialMediaUploadError("Media upload cancelled.")));
    };

    if (options.signal?.aborted) {
      abort();
      return;
    }
    options.signal?.addEventListener("abort", abort, { once: true });
    xhr.open("POST", "/api/social/media");
    xhr.responseType = "json";
    xhr.upload.onprogress = (event) => {
      if (!event.lengthComputable || event.total === 0) return;
      options.onProgress?.({ loaded: event.loaded, total: event.total, percent: Math.round((event.loaded / event.total) * 100) });
    };
    xhr.onerror = () => finish(() => reject(new SocialMediaUploadError("Media upload failed. Try again.")));
    xhr.onabort = () => finish(() => reject(new SocialMediaUploadError("Media upload cancelled.")));
    xhr.onload = () => finish(() => {
      const body = xhr.response && typeof xhr.response === "object"
        ? xhr.response
        : (() => { try { return JSON.parse(xhr.responseText) as unknown; } catch { return null; } })();
      if (xhr.status < 200 || xhr.status >= 300 || !body || typeof body !== "object") {
        reject(new SocialMediaUploadError("Media upload failed. Try again."));
        return;
      }
      const asset = body as Partial<SocialMediaAsset>;
      if (!asset.assetReferenceId || !asset.state) {
        reject(new SocialMediaUploadError("The upload response was invalid. Try again."));
        return;
      }
      resolve({
        assetReferenceId: asset.assetReferenceId,
        deliveryUrl: asset.deliveryUrl ?? null,
        mimeType: asset.mimeType ?? null,
        sizeBytes: asset.sizeBytes ?? file.size,
        state: asset.state,
      });
    });
    const formData = new FormData();
    formData.append("file", file);
    xhr.send(formData);
  });
}
