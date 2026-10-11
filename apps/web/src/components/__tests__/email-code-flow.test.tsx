import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import {
  createMockUseAuth,
  renderWithUser,
  type MockUseAuthReturn,
} from "@/test/auth-test-helpers";

const mocks = vi.hoisted(() => ({ requestEmailCode: vi.fn() }));
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
vi.mock("@/lib/auth/email-code-request", () => ({
  requestEmailCode: mocks.requestEmailCode,
}));

const { EmailCodeFlow } = await import("@/components/email-code-flow");

const messages = {
  request: {
    title: "Sign in with an email code",
    description: "Enter your email address.",
    emailLabel: "Email address",
    emailPlaceholder: "name@example.com",
    submit: "Send sign-in code",
    pending: "Sending...",
    successTitle: "Check your email",
    successDescription: "If an account matches, we sent a code.",
    error: "Could not send a code.",
  },
  consume: {
    title: "Enter your sign-in code",
    description: "Enter the six-digit code we sent to",
    codeLabel: "Sign-in code",
    codePlaceholder: "123456",
    submit: "Sign in",
    pending: "Checking...",
    useDifferentEmail: "Use a different email",
    errorTitle: "This code cannot be used",
    error: "The code expired or was already used.",
  },
  backToSignIn: "Back to sign in",
};

describe("EmailCodeFlow", () => {
  beforeEach(() => {
    mockAuth = createMockUseAuth();
    mocks.requestEmailCode.mockReset().mockResolvedValue(true);
  });

  it("continues the pending MFA challenge without resending the consumed email code", async () => {
    const { user, rerender } = renderWithUser(
      <EmailCodeFlow redirectTo="/workspace" apiUrl="https://api.example.test" messages={messages} />,
    );
    await user.type(screen.getByLabelText("Email address"), "player@example.com");
    await user.click(screen.getByRole("button", { name: "Send sign-in code" }));
    await user.type(await screen.findByLabelText("Sign-in code"), "123456");
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    mockAuth.mfaChallenge = { mfaToken: "x".repeat(43), availableMethods: ["Totp", "BackupCode"] };
    rerender(<EmailCodeFlow redirectTo="/workspace" apiUrl="https://api.example.test" messages={messages} />);
    expect(screen.queryByLabelText("Sign-in code")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Password")).not.toBeInTheDocument();
    await user.type(screen.getByLabelText("Authentication code"), "654321");
    await user.click(screen.getByRole("button", { name: "Verify and sign in" }));
    expect(mockAuth.signIn).toHaveBeenLastCalledWith("credentials", {
      mfaToken: "x".repeat(43), method: "Totp", code: "654321", redirectTo: "/workspace",
    });
    expect(mockAuth.signIn).toHaveBeenCalledTimes(2);
  });

  it("requests a code and advances to the code-entry step", async () => {
    const { user } = renderWithUser(
      <EmailCodeFlow
        apiUrl="https://api.example.test"
        messages={messages}
      />,
    );

    await user.type(
      screen.getByLabelText("Email address"),
      "player@example.com",
    );
    await user.click(screen.getByRole("button", { name: "Send sign-in code" }));

    await waitFor(() =>
      expect(mocks.requestEmailCode).toHaveBeenCalledWith(
        "player@example.com",
        "https://api.example.test",
      ),
    );
    expect(
      await screen.findByRole("button", { name: "Sign in" }),
    ).toBeInTheDocument();
    expect(screen.getByText(/player@example\.com/)).toBeInTheDocument();
  });

  it("shows a generic retry message when the code cannot be sent", async () => {
    mocks.requestEmailCode.mockResolvedValue(false);
    const { user } = renderWithUser(
      <EmailCodeFlow
        apiUrl="https://api.example.test"
        messages={messages}
      />,
    );

    await user.type(
      screen.getByLabelText("Email address"),
      "player@example.com",
    );
    await user.click(screen.getByRole("button", { name: "Send sign-in code" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      messages.request.error,
    );
    expect(screen.queryByLabelText("Sign-in code")).not.toBeInTheDocument();
  });

  it("signs in with the entered code and redirects", async () => {
    const { user } = renderWithUser(
      <EmailCodeFlow
        redirectTo="/workspace"
        apiUrl="https://api.example.test"
        messages={messages}
      />,
    );

    await user.type(
      screen.getByLabelText("Email address"),
      "player@example.com",
    );
    await user.click(screen.getByRole("button", { name: "Send sign-in code" }));
    await user.type(await screen.findByLabelText("Sign-in code"), "123456");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() =>
      expect(mockAuth.signIn).toHaveBeenCalledWith("credentials", {
        emailCode: "123456",
        emailCodeEmail: "player@example.com",
        redirectTo: "/workspace",
      }),
    );
  });

  it("keeps the submit button disabled until six digits are entered", async () => {
    const { user } = renderWithUser(
      <EmailCodeFlow
        apiUrl="https://api.example.test"
        messages={messages}
      />,
    );

    await user.type(
      screen.getByLabelText("Email address"),
      "player@example.com",
    );
    await user.click(screen.getByRole("button", { name: "Send sign-in code" }));
    const codeInput = await screen.findByLabelText("Sign-in code");
    const submit = screen.getByRole("button", { name: "Sign in" });

    expect(submit).toBeDisabled();
    await user.type(codeInput, "12345");
    expect(submit).toBeDisabled();
    await user.type(codeInput, "6");
    expect(submit).toBeEnabled();
  });

  it("offers a retry after an invalid or expired code without leaking backend details", async () => {
    mockAuth = createMockUseAuth({
      signIn: vi.fn().mockRejectedValue(new Error("private backend detail")),
    });
    const { user } = renderWithUser(
      <EmailCodeFlow
        apiUrl="https://api.example.test"
        messages={messages}
      />,
    );

    await user.type(
      screen.getByLabelText("Email address"),
      "player@example.com",
    );
    await user.click(screen.getByRole("button", { name: "Send sign-in code" }));
    await user.type(await screen.findByLabelText("Sign-in code"), "000000");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      messages.consume.error,
    );
    expect(
      screen.queryByText("private backend detail"),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Use a different email" }),
    ).toBeInTheDocument();
  });

  it("returns to the email step when a different email is requested", async () => {
    const { user } = renderWithUser(
      <EmailCodeFlow
        apiUrl="https://api.example.test"
        messages={messages}
      />,
    );

    await user.type(
      screen.getByLabelText("Email address"),
      "player@example.com",
    );
    await user.click(screen.getByRole("button", { name: "Send sign-in code" }));
    await user.click(
      await screen.findByRole("button", { name: "Use a different email" }),
    );

    expect(
      await screen.findByRole("button", { name: "Send sign-in code" }),
    ).toBeInTheDocument();
    expect(screen.queryByLabelText("Sign-in code")).not.toBeInTheDocument();
    expect(mocks.requestEmailCode).toHaveBeenCalledTimes(1);
  });
});
