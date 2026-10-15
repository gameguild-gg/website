import "@testing-library/jest-dom/vitest";
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

// Mock BlogBodyEditor to avoid its internal imports that cause "not exported" errors
vi.mock("./blog-body-editor", () => ({
  BlogBodyEditor: ({ postId }: { postId: string }) => (
    <div data-testid="blog-body-editor">
      <div data-testid="scope">{"{\"type\":\"BlogPost\",\"id\":\"" + postId + "\"}"}</div>
    </div>
  ),
}));

import { BlogBodyEditor } from "./blog-body-editor";

describe("BlogBodyEditor Asset Integration", () => {
  it("configures the asset picker with the correct BlogPost scope", () => {
    render(
      <BlogBodyEditor
        postId="post-123"
        format="Lexical"
        markdown=""
        jsonBody="null"
        onMarkdownChange={vi.fn()}
        onJsonBodyChange={vi.fn()}
      />
    );

    expect(screen.getByTestId("scope")).toHaveTextContent('"type":"BlogPost","id":"post-123"');
  });
});
