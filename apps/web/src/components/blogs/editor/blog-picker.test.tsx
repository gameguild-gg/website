import "@testing-library/jest-dom/vitest";
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

// 1. Mock the problematic deep import FIRST
vi.mock("@game-guild/lexical-surface/features/media", () => ({
  MediaPlugin: () => <div data-testid="media-plugin" />,
  INSERT_MEDIA_LEXICAL_COMMAND: "insert-media",
}));

// 2. Mock the main surface
vi.mock("@game-guild/lexical-surface", () => ({
  LexicalSurface: ({ assetScope }: any) => (
    <div data-testid="lexical-surface">
      <span data-testid="scope">{JSON.stringify(assetScope)}</span>
    </div>
  ),
}));

// 3. Mock the assets react
vi.mock("@game-guild/assets/react", () => ({
  AssetsProvider: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  AssetPickerDialog: ({ onSelect, assetScope }: { onSelect: (uri: string) => void, assetScope: any }) => (
    <div data-testid="asset-picker">
      <button onClick={() => onSelect("asset://test-asset-123")}>Select Asset</button>
      <span data-testid="scope">{JSON.stringify(assetScope)}</span>
    </div>
  ),
}));

// 4. Now import the component
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
