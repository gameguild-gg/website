import "@testing-library/jest-dom/vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  resolveRedirect: vi.fn((value: string | undefined) => value ?? "/"),
}));

vi.mock("next-intl/server", () => ({
  getTranslations: async (namespace: string) => (key: string) =>
    `${namespace}.${key}`,
}));
vi.mock("@/lib/auth/cross-domain-auth", () => ({
  resolveAllowedAuthRedirect: mocks.resolveRedirect,
}));
vi.mock("@/components/magic-link-flow", () => ({
  MagicLinkFlow: ({
    token,
    redirectTo,
    locale,
    apiUrl,
  }: {
    token: string | null;
    redirectTo: string;
    locale: string;
    apiUrl: string;
  }) => (
    <div
      data-testid="magic-link-flow"
      data-token={token ?? ""}
      data-redirect={redirectTo}
      data-locale={locale}
      data-api-url={apiUrl}
    />
  ),
}));

import MagicLinkPage from "./page";

async function renderPage(
  searchParams: Record<string, string | string[] | undefined>,
) {
  const page = await MagicLinkPage({
    params: Promise.resolve({ locale: "pt-BR" }),
    searchParams: Promise.resolve(searchParams),
  } as never);
  return render(page);
}

describe("magic-link page", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("renders the request flow when the email link does not include a token", async () => {
    await renderPage({});

    expect(screen.getByTestId("magic-link-flow")).toHaveAttribute(
      "data-token",
      "",
    );
    expect(screen.getByTestId("magic-link-flow")).toHaveAttribute(
      "data-redirect",
      "/",
    );
    expect(screen.getByTestId("magic-link-flow")).toHaveAttribute(
      "data-locale",
      "pt-BR",
    );
  });

  it("passes the email token and validated redirect into the consume flow", async () => {
    await renderPage({ token: "one-time-token", redirectTo: "/workspace" });

    expect(screen.getByTestId("magic-link-flow")).toHaveAttribute(
      "data-token",
      "one-time-token",
    );
    expect(screen.getByTestId("magic-link-flow")).toHaveAttribute(
      "data-redirect",
      "/workspace",
    );
    expect(mocks.resolveRedirect).toHaveBeenCalledWith("/workspace");
  });

  it("uses the first token when repeated query parameters are supplied", async () => {
    await renderPage({ token: ["first-token", "second-token"] });

    expect(screen.getByTestId("magic-link-flow")).toHaveAttribute(
      "data-token",
      "first-token",
    );
  });
});
