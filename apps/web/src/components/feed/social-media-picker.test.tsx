import "@testing-library/jest-dom/vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { SocialMediaPicker } from "./social-media-picker";

describe("SocialMediaPicker", () => {
  beforeEach(() => {
    URL.createObjectURL = vi.fn(() => "blob:selected-media");
    URL.revokeObjectURL = vi.fn();
  });
  afterEach(cleanup);

  it("rejects an unsupported MIME type before selection", () => {
    render(<SocialMediaPicker value={null} onChange={vi.fn()} />);
    fireEvent.change(screen.getByLabelText(/add photo or video/i), {
      target: { files: [new File(["text"], "notes.txt", { type: "text/plain" })] },
    });
    expect(screen.getByRole("alert")).toHaveTextContent(/jpeg, png, webp, gif, or mp4/i);
  });

  it("rejects images larger than 10 MiB", () => {
    render(<SocialMediaPicker value={null} onChange={vi.fn()} />);
    const oversized = new File([new Uint8Array(10 * 1024 * 1024 + 1)], "too-big.png", { type: "image/png" });
    fireEvent.change(screen.getByLabelText(/add photo or video/i), { target: { files: [oversized] } });
    expect(screen.getByRole("alert")).toHaveTextContent(/up to 10 mb/i);
  });

  it("rejects MP4 files larger than 100 MiB", () => {
    render(<SocialMediaPicker value={null} onChange={vi.fn()} />);
    const oversized = new File([new Uint8Array(100 * 1024 * 1024 + 1)], "too-big.mp4", { type: "video/mp4" });
    fireEvent.change(screen.getByLabelText(/add photo or video/i), { target: { files: [oversized] } });
    expect(screen.getByRole("alert")).toHaveTextContent(/up to 100 mb/i);
  });

  it("releases the object URL when selected media is removed", () => {
    const onChange = vi.fn();
    const view = render(<SocialMediaPicker value={null} onChange={onChange} />);
    const file = new File(["png"], "build.png", { type: "image/png" });
    fireEvent.change(screen.getByLabelText(/add photo or video/i), { target: { files: [file] } });
    const selected = onChange.mock.calls[0]?.[0];
    view.rerender(<SocialMediaPicker value={selected} onChange={onChange} />);
    fireEvent.click(screen.getByRole("button", { name: /remove media/i }));
    view.rerender(<SocialMediaPicker value={null} onChange={onChange} />);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:selected-media");
  });

  it("releases previous and current object URLs on replacement and unmount", () => {
    const view = render(<SocialMediaPicker value={{ file: new File(["a"], "a.png", { type: "image/png" }), previewUrl: "blob:first", kind: "image" }} onChange={vi.fn()} />);
    view.rerender(<SocialMediaPicker value={{ file: new File(["b"], "b.png", { type: "image/png" }), previewUrl: "blob:second", kind: "image" }} onChange={vi.fn()} />);
    view.unmount();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:first");
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:second");
  });
});
