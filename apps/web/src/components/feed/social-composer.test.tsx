import "@testing-library/jest-dom/vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import * as React from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  createSocialPost: vi.fn(),
  getSocialMediaStatus: vi.fn(),
  uploadSocialMediaWithProgress: vi.fn(),
}));

vi.mock("@/lib/feed/actions", () => mocks);
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
  afterEach(cleanup);

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
});
