import "@testing-library/jest-dom/vitest";
import { render, screen } from "@testing-library/react";
import type { ComponentProps } from "react";
import { describe, expect, it, vi } from "vitest";

vi.mock("@/i18n/navigation", () => ({
  Link: ({ href, children, ...props }: ComponentProps<"a">) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock("next/image", () => ({
  default: ({ src, alt, width, height, className }: ComponentProps<"img">) => (
    <span
      data-testid="brand-mark"
      data-src={src}
      data-alt={alt}
      data-width={width}
      data-height={height}
      className={className}
    />
  ),
}));

import { GameGuildLogo } from "./game-guild-logo";

describe("GameGuildLogo", () => {
  it("uses the repository brand mark and links to the selected locale", () => {
    const { container } = render(<GameGuildLogo locale="pt-BR" />);

    expect(screen.getByRole("link", { name: "GameGuild" })).toHaveAttribute("href", "/pt-BR");
    const mark = screen.getByTestId("brand-mark");
    expect(mark).toHaveAttribute("data-src", "/assets/brand/gameguild-mark.svg");
    expect(mark).toHaveAttribute("data-alt", "");
    expect(mark).toHaveAttribute("data-width", "36");
    expect(mark).toHaveAttribute("data-height", "36");
    expect(container).toContainElement(mark);
  });
});
