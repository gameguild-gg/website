import "@testing-library/jest-dom/vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import * as React from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  createSocialPost: vi.fn(),
  getSocialMediaStatus: vi.fn(),
  uploadSocialMediaWithProgress: vi.fn(),
  hydrateSocialPost: vi.fn(),
  SocialPostHydrationError: class SocialPostHydrationError extends Error { constructor(readonly postId: string) { super("pending"); } },
}));

vi.mock("@/lib/feed/actions", () => mocks);
vi.mock("@/lib/feed/errors", () => ({ SocialPostHydrationError: mocks.SocialPostHydrationError }));
vi.mock("@/lib/feed/social-media-upload", () => ({ uploadSocialMediaWithProgress: mocks.uploadSocialMediaWithProgress }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh: vi.fn() }) }));
import { SocialComposer } from "./social-composer";

describe("SocialComposer", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.uploadSocialMediaWithProgress.mockResolvedValue({
      assetReferenceId: "asset-1",
      deliveryUrl: "/v1/assets/asset-1/content",
      mimeType: "image/png",
      sizeBytes: 3,
      state: "Ready",
    });
    mocks.createSocialPost.mockResolvedValue({ id: "post-1", kind: "Post" });
    URL.createObjectURL = vi.fn(() => "blob:preview");
    URL.revokeObjectURL = vi.fn();
  });
  afterEach(() => {
    vi.useRealTimers();
    cleanup();
  });

  it("uploads first-party media and publishes its asset reference", async () => {
    render(<SocialComposer userName="Ada Builder" />);
    fireEvent.click(screen.getByRole("button", { name: /share your progress/i }));
    fireEvent.change(screen.getByPlaceholderText(/what are you building/i), {
      target: { value: "New build #indiedev" },
    });
    const file = new File(["png"], "build.png", { type: "image/png" });
    fireEvent.change(screen.getByLabelText(/add photo or video/i), {
      target: { files: [file] },
    });
    fireEvent.click(screen.getByRole("button", { name: /^publish$/i }));

    await waitFor(() => expect(mocks.uploadSocialMediaWithProgress).toHaveBeenCalledWith(file, expect.objectContaining({ signal: expect.any(AbortSignal) })));
    await waitFor(() =>
      expect(mocks.createSocialPost).toHaveBeenCalledWith({
        content: "New build #indiedev",
        visibility: "Public",
        assetReferenceId: "asset-1",
        tags: ["indiedev"],
      }),
    );
  });

  it("rejects unsupported files before upload", async () => {
    render(<SocialComposer userName="Ada Builder" />);
    fireEvent.click(screen.getByRole("button", { name: /share your progress/i }));
    const file = new File(["bad"], "notes.txt", { type: "text/plain" });
    fireEvent.change(screen.getByLabelText(/add photo or video/i), {
      target: { files: [file] },
    });
    expect(await screen.findByText(/choose a jpeg, png, webp, gif, or mp4/i)).toBeInTheDocument();
    expect(mocks.uploadSocialMediaWithProgress).not.toHaveBeenCalled();
  });

  it("previews selected media and releases its object URL when removed", async () => {
    render(<SocialComposer userName="Ada Builder" />);
    fireEvent.click(screen.getByRole("button", { name: /share your progress/i }));
    const file = new File(["png"], "build.png", { type: "image/png" });
    fireEvent.change(screen.getByLabelText(/add photo or video/i), { target: { files: [file] } });

    expect(screen.getByRole("img", { name: /selected media preview/i })).toHaveAttribute("src", "blob:preview");
    fireEvent.click(screen.getByRole("button", { name: /remove media/i }));
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:preview");
  });

  it("does not submit the same draft twice while publishing", async () => {
    let release!: (value: { id: string; kind: string }) => void;
    mocks.createSocialPost.mockImplementationOnce(() => new Promise((resolve) => { release = resolve; }));
    render(<SocialComposer userName="Ada Builder" />);
    fireEvent.click(screen.getByRole("button", { name: /share your progress/i }));
    fireEvent.change(screen.getByPlaceholderText(/what are you building/i), { target: { value: "One post" } });
    const form = screen.getByPlaceholderText(/what are you building/i).closest("form")!;
    fireEvent.submit(form);
    fireEvent.submit(form);

    expect(mocks.createSocialPost).toHaveBeenCalledTimes(1);
    release({ id: "post-1" });
    await waitFor(() => expect(screen.getByRole("button", { name: /share your progress/i })).toBeInTheDocument());
  });

  it("hands the authoritative created feed item to its parent exactly once", async () => {
    const onPublished = vi.fn();
    render(React.createElement(SocialComposer, { userName: "Ada Builder", onPublished } as never));
    fireEvent.click(screen.getByRole("button", { name: /share your progress/i }));
    fireEvent.change(screen.getByPlaceholderText(/what are you building/i), { target: { value: "One post" } });
    fireEvent.click(screen.getByRole("button", { name: /^publish$/i }));

    await waitFor(() => expect(onPublished).toHaveBeenCalledWith({ id: "post-1", kind: "Post" }));
    expect(onPublished).toHaveBeenCalledTimes(1);
  });

  it("hydrates a committed post on retry without issuing a second POST", async () => {
    const onPublished = vi.fn();
    mocks.createSocialPost.mockRejectedValueOnce(new mocks.SocialPostHydrationError("post-1"));
    mocks.hydrateSocialPost.mockResolvedValueOnce({ id: "post-1", kind: "Post" });
    render(React.createElement(SocialComposer, { userName: "Ada Builder", onPublished } as never));
    fireEvent.click(screen.getByRole("button", { name: /share your progress/i }));
    fireEvent.change(screen.getByPlaceholderText(/what are you building/i), { target: { value: "One post" } });
    fireEvent.click(screen.getByRole("button", { name: /^publish$/i }));
    expect(await screen.findByRole("button", { name: /retry feed update/i })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /retry feed update/i }));
    await waitFor(() => expect(onPublished).toHaveBeenCalledWith({ id: "post-1", kind: "Post" }));
    expect(mocks.createSocialPost).toHaveBeenCalledTimes(1);
    expect(mocks.hydrateSocialPost).toHaveBeenCalledWith("post-1");
  });

  it("cancels an upload and retries without losing the valid draft", async () => {
    let uploadSignal: AbortSignal | undefined;
    mocks.uploadSocialMediaWithProgress.mockImplementationOnce(
      (_file: File, options: { signal: AbortSignal }) =>
        new Promise((_resolve, reject) => {
          uploadSignal = options.signal;
          options.signal.addEventListener(
            "abort",
            () => reject(new Error("Media upload cancelled.")),
            { once: true },
          );
        }),
    );

    render(<SocialComposer userName="Ada Builder" />);
    fireEvent.click(screen.getByRole("button", { name: /share your progress/i }));
    const draft = screen.getByPlaceholderText(/what are you building/i);
    fireEvent.change(draft, { target: { value: "Keep this draft" } });
    fireEvent.change(screen.getByLabelText(/add photo or video/i), {
      target: {
        files: [new File(["png"], "build.png", { type: "image/png" })],
      },
    });
    fireEvent.click(screen.getByRole("button", { name: /^publish$/i }));
    fireEvent.click(
      await screen.findByRole("button", { name: /cancel upload/i }),
    );

    await waitFor(() => expect(uploadSignal?.aborted).toBe(true));
    expect(await screen.findByText(/media upload cancelled/i)).toBeInTheDocument();
    expect(draft).toHaveValue("Keep this draft");
    expect(
      screen.getByRole("img", { name: /selected media preview/i }),
    ).toBeInTheDocument();
    expect(mocks.createSocialPost).not.toHaveBeenCalled();

    mocks.uploadSocialMediaWithProgress.mockResolvedValueOnce({
      assetReferenceId: "asset-1",
      deliveryUrl: "/v1/assets/asset-1/content",
      mimeType: "image/png",
      sizeBytes: 3,
      state: "Ready",
    });
    fireEvent.click(screen.getByRole("button", { name: /retry upload/i }));

    await waitFor(() => expect(mocks.createSocialPost).toHaveBeenCalledTimes(1));
    expect(mocks.uploadSocialMediaWithProgress).toHaveBeenCalledTimes(2);
  });

  it("keeps rejected processing media available for an explicit retry", async () => {
    mocks.uploadSocialMediaWithProgress.mockResolvedValueOnce({
      assetReferenceId: "asset-processing",
      deliveryUrl: null,
      mimeType: "image/png",
      sizeBytes: 3,
      state: "Processing",
    });
    mocks.getSocialMediaStatus.mockResolvedValueOnce({
      assetReferenceId: "asset-processing",
      deliveryUrl: null,
      mimeType: "image/png",
      sizeBytes: 3,
      state: "Rejected",
    });

    render(<SocialComposer userName="Ada Builder" />);
    fireEvent.click(screen.getByRole("button", { name: /share your progress/i }));
    fireEvent.change(screen.getByLabelText(/add photo or video/i), {
      target: {
        files: [new File(["png"], "build.png", { type: "image/png" })],
      },
    });
    fireEvent.click(screen.getByRole("button", { name: /^publish$/i }));

    expect(await screen.findByText(/processing media/i)).toBeInTheDocument();
    expect(
      await screen.findByText(/rejected during processing/i, {}, { timeout: 2_000 }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("img", { name: /selected media preview/i }),
    ).toBeInTheDocument();
    expect(mocks.createSocialPost).not.toHaveBeenCalled();

    mocks.uploadSocialMediaWithProgress.mockResolvedValueOnce({
      assetReferenceId: "asset-ready",
      deliveryUrl: "/v1/assets/asset-ready/content",
      mimeType: "image/png",
      sizeBytes: 3,
      state: "Ready",
    });
    fireEvent.click(screen.getByRole("button", { name: /retry upload/i }));
    await waitFor(() => expect(mocks.createSocialPost).toHaveBeenCalledTimes(1));
  });

  it("bounds processing polls and leaves timed-out media retryable", async () => {
    vi.useFakeTimers();
    mocks.uploadSocialMediaWithProgress.mockResolvedValueOnce({
      assetReferenceId: "asset-processing",
      deliveryUrl: null,
      mimeType: "image/png",
      sizeBytes: 3,
      state: "Processing",
    });
    mocks.getSocialMediaStatus.mockResolvedValue({
      assetReferenceId: "asset-processing",
      deliveryUrl: null,
      mimeType: "image/png",
      sizeBytes: 3,
      state: "Processing",
    });

    render(<SocialComposer userName="Ada Builder" />);
    fireEvent.click(screen.getByRole("button", { name: /share your progress/i }));
    fireEvent.change(screen.getByLabelText(/add photo or video/i), {
      target: {
        files: [new File(["png"], "build.png", { type: "image/png" })],
      },
    });
    fireEvent.click(screen.getByRole("button", { name: /^publish$/i }));

    await act(async () => {
      await Promise.resolve();
      await Promise.resolve();
    });
    expect(screen.getByText(/processing media/i)).toBeInTheDocument();
    for (let attempt = 0; attempt < 20; attempt += 1) {
      await act(async () => {
        await vi.runOnlyPendingTimersAsync();
      });
    }

    expect(mocks.getSocialMediaStatus).toHaveBeenCalledTimes(20);
    expect(screen.getByText(/processing timed out/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /retry upload/i })).toBeInTheDocument();
    expect(
      screen.getByRole("img", { name: /selected media preview/i }),
    ).toBeInTheDocument();
    expect(mocks.createSocialPost).not.toHaveBeenCalled();
  });

  it("renders byte-level upload progress accessibly", async () => {
    let release!: (value: { assetReferenceId: string; state: "Ready"; sizeBytes: number; deliveryUrl: null; mimeType: string }) => void;
    mocks.uploadSocialMediaWithProgress.mockImplementationOnce((_file: File, options: { onProgress: (progress: { loaded: number; total: number; percent: number }) => void }) => {
      options.onProgress({ loaded: 50, total: 100, percent: 50 });
      return new Promise((resolve) => { release = resolve; });
    });
    render(<SocialComposer userName="Ada Builder" />);
    fireEvent.click(screen.getByRole("button", { name: /share your progress/i }));
    fireEvent.change(screen.getByLabelText(/add photo or video/i), { target: { files: [new File(["png"], "build.png", { type: "image/png" })] } });
    fireEvent.click(screen.getByRole("button", { name: /^publish$/i }));
    expect(await screen.findByRole("progressbar", { name: /media upload progress/i })).toHaveAttribute("aria-valuenow", "50");
    expect(screen.getByText(/uploading 50 of 100 bytes/i)).toBeInTheDocument();
    await act(async () => { release({ assetReferenceId: "asset-1", state: "Ready", sizeBytes: 100, deliveryUrl: null, mimeType: "image/png" }); });
  });

  it("shows 1000-character feedback and normalizes at most ten tags", async () => {
    render(<SocialComposer userName="Ada Builder" />);
    fireEvent.click(screen.getByRole("button", { name: /share your progress/i }));
    const text = `${"x".repeat(1000)} #ONE #one #two #three #four #five #six #seven #eight #nine #ten #eleven`;
    fireEvent.change(screen.getByPlaceholderText(/what are you building/i), { target: { value: text } });
    expect(screen.getByText(`${text.length}/4000 characters`)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /^publish$/i }));
    await waitFor(() => expect(mocks.createSocialPost).toHaveBeenCalledWith(expect.objectContaining({ tags: ["one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten"] })));
  });
});
