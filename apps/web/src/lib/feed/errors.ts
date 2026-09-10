import type { ApiError } from "@game-guild/client";

export class FeedMutationError extends Error {
  readonly status: number;
  readonly code: string;

  constructor(error: Partial<ApiError> & { message?: string }) {
    super(error.message || "The social action failed.");
    this.name = "FeedMutationError";
    this.status = error.status ?? 0;
    this.code = error.code ?? "UNKNOWN";
  }
}
