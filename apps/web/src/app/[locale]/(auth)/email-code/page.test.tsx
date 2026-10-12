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
vi.mock("@/components/email-code-flow", () => ({
  EmailCodeFlow: ({
    redirectTo,
    apiUrl,
  }: {
    redirectTo: string;
    apiUrl: string;
  }) => (
    <div
      data-testid="email-code-flow"
      data-redirect={redirectTo}
      data-api-url={apiUrl}
    />
  ),
}));

import EmailCodePage from "./page";

async function renderPage(
  searchParams: Record<string, string | string[] | undefined>,
) {
  const page = await EmailCodePage({
    searchParams: Promise.resolve(searchParams),
  } as never);
  return render(page);
}

describe("email-code page", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("renders the code flow with the default redirect when no query is supplied", async () => {
    await renderPage({});

    expect(screen.getByTestId("email-code-flow")).toHaveAttribute(
      "data-redirect",
      "/",
    );
    expect(mocks.resolveRedirect).toHaveBeenCalledWith(undefined);
  });

  it("passes the validated redirect into the code flow", async () => {
    await renderPage({ redirectTo: "/workspace" });

    expect(screen.getByTestId("email-code-flow")).toHaveAttribute(
      "data-redirect",
      "/workspace",
    );
    expect(mocks.resolveRedirect).toHaveBeenCalledWith("/workspace");
  });

  it("uses the first redirect when repeated query parameters are supplied", async () => {
    await renderPage({ redirectTo: ["/first", "/second"] });

    expect(screen.getByTestId("email-code-flow")).toHaveAttribute(
      "data-redirect",
      "/first",
    );
  });
});
