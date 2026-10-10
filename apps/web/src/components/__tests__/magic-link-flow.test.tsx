import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import {
  createMockUseAuth,
  renderWithUser,
  type MockUseAuthReturn,
} from "@/test/auth-test-helpers";

const mocks = vi.hoisted(() => ({ requestMagicLink: vi.fn() }));
let mockAuth: MockUseAuthReturn;

vi.mock("@game-guild/client/react", () => ({
  useAuth: () => mockAuth,
}));
vi.mock("@/i18n/navigation", () => ({
  Link: ({
    children,
    href,
    ...rest
  }: React.ComponentProps<"a"> & { href: string }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));
vi.mock("@/lib/auth/magic-link-request", () => ({
  requestMagicLink: mocks.requestMagicLink,
}));

const { MagicLinkFlow } = await import("@/components/magic-link-flow");

const messages = {
  request: {
    title: "Sign in with an email link",
    description: "Enter your email address.",
    emailLabel: "Email address",
    emailPlaceholder: "name@example.com",
    submit: "Send sign-in link",
    pending: "Sending...",
    successTitle: "Check your email",
    successDescription: "If an account matches, we sent a link.",
    error: "Could not send a link.",
  },
  consume: {
    title: "Signing you in",
    pending: "Checking your link...",
    success: "Signed in. Redirecting...",
    errorTitle: "This link cannot be used",
    errorDescription: "The link expired or was already used.",
  },
  backToSignIn: "Back to sign in",
  requestAnother: "Request another sign-in link",
};

describe("MagicLinkFlow", () => {
  beforeEach(() => {
    mockAuth = createMockUseAuth();
    mocks.requestMagicLink.mockReset().mockResolvedValue(true);
  });

  it("continues MFA without consuming the one-time link a second time", async () => {
    const { user, rerender } = renderWithUser(
      <MagicLinkFlow token="verified-link" redirectTo="/workspace" apiUrl="https://api.example.test" messages={messages} />,
    );
    await waitFor(() => expect(mockAuth.signIn).toHaveBeenCalledTimes(1));
    mockAuth.mfaChallenge = { mfaToken: "x".repeat(43), availableMethods: ["Totp", "BackupCode"] };
    rerender(<MagicLinkFlow token="verified-link" redirectTo="/workspace" apiUrl="https://api.example.test" messages={messages} />);
    expect(screen.queryByLabelText("Password")).not.toBeInTheDocument();
    await user.type(screen.getByLabelText("Authentication code"), "654321");
    await user.click(screen.getByRole("button", { name: "Verify and sign in" }));
    expect(mockAuth.signIn).toHaveBeenLastCalledWith("credentials", {
      mfaToken: "x".repeat(43), method: "Totp", code: "654321", redirectTo: "/workspace",
    });
    expect(mockAuth.signIn).toHaveBeenCalledTimes(2);
  });

  it("requests a link and shows the same confirmation for every email address", async () => {
    const { user } = renderWithUser(
      <MagicLinkFlow
        token={null}
        apiUrl="https://api.example.test"
        messages={messages}
      />,
    );

    await user.type(
      screen.getByLabelText("Email address"),
      "player@example.com",
    );
    await user.click(screen.getByRole("button", { name: "Send sign-in link" }));

    await waitFor(() =>
      expect(mocks.requestMagicLink).toHaveBeenCalledWith(
        "player@example.com",
        "https://api.example.test",
      ),
    );
    expect(await screen.findByRole("status")).toHaveTextContent(
      messages.request.successDescription,
    );
    expect(
      screen.getByRole("link", { name: "Back to sign in" }),
    ).toHaveAttribute("href", "/sign-in");
  });

  it("shows a generic retry message when the request cannot be sent", async () => {
    mocks.requestMagicLink.mockResolvedValue(false);
    const { user } = renderWithUser(
      <MagicLinkFlow
        token={null}
        apiUrl="https://api.example.test"
        messages={messages}
      />,
    );

    await user.type(
      screen.getByLabelText("Email address"),
      "player@example.com",
    );
    await user.click(screen.getByRole("button", { name: "Send sign-in link" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      messages.request.error,
    );
  });

  it("consumes the token once and redirects after authentication", async () => {
    const { rerender } = renderWithUser(
      <MagicLinkFlow
        token="one-time-token"
        redirectTo="/workspace"
        apiUrl="https://api.example.test"
        messages={messages}
      />,
    );

    await waitFor(() =>
      expect(mockAuth.signIn).toHaveBeenCalledWith("credentials", {
        magicLinkToken: "one-time-token",
        redirectTo: "/workspace",
      }),
    );
    expect(await screen.findByRole("status")).toHaveTextContent(
      messages.consume.success,
    );

    rerender(
      <MagicLinkFlow
        token="one-time-token"
        redirectTo="/workspace"
        apiUrl="https://api.example.test"
        messages={messages}
      />,
    );
    expect(mockAuth.signIn).toHaveBeenCalledTimes(1);
  });

  it("offers a new link after an expired or already-used token", async () => {
    mockAuth = createMockUseAuth({
      signIn: vi.fn().mockRejectedValue(new Error("private backend detail")),
    });
    renderWithUser(
      <MagicLinkFlow
        token="expired-token"
        apiUrl="https://api.example.test"
        messages={messages}
      />,
    );

    expect(await screen.findByRole("alert")).toHaveTextContent(
      messages.consume.errorDescription,
    );
    expect(
      screen.queryByText("private backend detail"),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Request another sign-in link" }),
    ).toHaveAttribute("href", "/magic-link");
  });
});
