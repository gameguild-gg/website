/**
 * Credentials Provider
 *
 * Authenticates via email/password against the .NET backend's /v1/auth/sign-in endpoint.
 * This is the primary provider for local authentication.
 *
 * @example
 * ```typescript
 * import { GameGuildAuth } from '@game-guild/client/next';
 * import { CredentialsProvider } from '@game-guild/client/auth';
 *
 * export const { handlers, auth, signIn, signOut, signUp } = GameGuildAuth({
 *   providers: [
 *     CredentialsProvider({
 *       apiUrl: process.env.API_URL!,
 *     }),
 *   ],
 * });
 * ```
 */

import type { CredentialsProviderConfig, ProviderResult, SessionUser } from '../types.js';
import { AccountLockedError, AuthServiceUnavailableError, CredentialsSignInError, MfaRequiredError } from '../errors.js';
import { resolveAuthPermissions, resolveAuthRoles } from '../claims.js';

/**
 * Options for the credentials provider
 */
export interface CredentialsProviderOptions {
  /**
   * Backend API base URL.
   * If not provided, uses the apiUrl from the main config.
   */
  apiUrl?: string;

  /**
   * Sign-in endpoint path
   * @default '/v1/auth/sign-in'
   */
  signInPath?: string;

  /**
   * Custom authorize function override.
   * If provided, replaces the default .NET sign-in logic.
   */
  authorize?: CredentialsProviderConfig['authorize'];
}

/**
 * Create a credentials provider for email/password authentication.
 *
 * @param options - Provider configuration
 * @returns A credentials provider config
 */
export function CredentialsProvider(options: CredentialsProviderOptions = {}): CredentialsProviderConfig {
  const { signInPath = '/v1/auth/sign-in', authorize: customAuthorize } = options;

  return {
    id: 'credentials',
    name: 'Credentials',
    type: 'credentials',
    authorize:
      customAuthorize ??
      (async (credentials) => {
        // The apiUrl can come from options or from the main config
        // When called from the auth system, the apiUrl is injected
        const apiUrl = options.apiUrl || (credentials.__apiUrl as string);

        if (!apiUrl) {
          throw new CredentialsSignInError('API URL not configured. Set apiUrl in provider options or GameGuildAuth config.');
        }

        const email = credentials.email as string;
        const password = credentials.password as string;
        const tenantId = credentials.tenantId as string | undefined;
        // "Remember me" opt-in from the sign-in form: drives the backend's persistent
        // refresh-token lifetime and this client's cookie storage policy.
        const rememberMe = credentials.rememberMe === true;

        const completingMfa = Object.hasOwn(credentials, 'mfaToken');
        if (!completingMfa && (!email || !password)) {
          throw new CredentialsSignInError('Email and password are required');
        }

        let path = signInPath;
        let body: Record<string, unknown>;
        if (completingMfa) {
          const { mfaToken, method, code } = credentials;
          if (
            typeof mfaToken !== 'string' ||
            !/^[A-Za-z0-9_-]{43}$/.test(mfaToken) ||
            (method !== 'Totp' && method !== 'BackupCode') ||
            typeof code !== 'string' ||
            !code.trim()
          ) {
            throw new CredentialsSignInError('A valid MFA challenge, method and code are required');
          }
          path = '/v1/auth/mfa/sign-in/complete';
          body = { mfaToken, method, code };
        } else {
          body = { email, password };
          if (tenantId) body.tenantId = tenantId;
          if (rememberMe) body.rememberMe = true;
        }

        let response: Response;
        try {
          response = await fetch(`${apiUrl}${path}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
          });
        } catch (error) {
          throw new AuthServiceUnavailableError(
            'Authentication service is unreachable. Please check the API deployment.',
            error instanceof Error ? error : undefined,
          );
        }

        if (!response.ok) {
          const errorData = await response.json().catch(() => ({}));
          const message =
            ((errorData as Record<string, unknown>).message as string) || ((errorData as Record<string, unknown>).detail as string) || 'Invalid credentials';

          if (response.status >= 500) {
            throw new AuthServiceUnavailableError();
          }

          if (response.status === 423) {
            throw new AccountLockedError(message);
          }

          throw new CredentialsSignInError(message);
        }

        const data = (await response.json()) as Record<string, unknown>;

        // Check for MFA requirement
        if (data.requiresMfa) {
          throw new MfaRequiredError('Multi-factor authentication required', {
            mfaSessionId: data.mfaSessionId as string | undefined,
            mfaToken: typeof data.mfaToken === 'string' ? data.mfaToken : undefined,
            availableMethods: Array.isArray(data.availableMethods) ? data.availableMethods.filter((m): m is string => typeof m === 'string') : undefined,
          });
        }

        if (typeof data.accessToken !== 'string' || !data.accessToken.trim()) {
          throw new CredentialsSignInError('Authentication did not complete');
        }

        // Extract user info from the response
        const backendUser = data.user as Record<string, unknown> | undefined;
        const user: SessionUser = {
          /* v8 ignore next */
          id: (data.userId as string) || (backendUser?.id as string) || '',
          email: (data.email as string) || (backendUser?.email as string) || (completingMfa ? '' : email),
          name: (backendUser?.displayName as string) || (backendUser?.username as string) || null,
          image: (backendUser?.profilePictureUrl as string) || null,
          roles: resolveAuthRoles(data, backendUser),
          permissions: resolveAuthPermissions(data, backendUser),
        };

        const result: ProviderResult = {
          tokens: {
            accessToken: data.accessToken as string,
            refreshToken: data.refreshToken as string,
            expiresIn: data.expiresIn as number | undefined,
            accessTokenExpiresAt: data.accessTokenExpiresAt as string | undefined,
            refreshTokenExpiresAt: data.refreshTokenExpiresAt as string | undefined,
            tokenType: 'Bearer',
          },
          user,
          sessionId: data.sessionId as string | undefined,
          tenantId: data.tenantId as string | undefined,
          availableTenants: data.availableTenants as Array<{ id: string; name: string }> | undefined,
          rememberMe,
          ...(completingMfa && Array.isArray(data.mfaEnrollmentBackupCodes)
            ? { mfaEnrollmentBackupCodes: data.mfaEnrollmentBackupCodes.filter((c): c is string => typeof c === 'string') }
            : {}),
        };

        return result;
      }),
  };
}
