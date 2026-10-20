import { act, renderHook, waitFor } from "@testing-library/react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { toast } from "sonner"
import type { GoogleTokenClientConfig } from "../../lib/api/editor/google-drive-sdk"
import { useGoogleDriveAuth } from "./use-google-drive-auth"

vi.mock("sonner", () => ({ toast: { error: vi.fn(), success: vi.fn() } }))

const now = 1792458000000
const token = "synthetic-google-drive-token"
let deliver: (config: GoogleTokenClientConfig) => void
const requestAccessToken = vi.fn<(config?: { prompt?: string }) => void>()
const initTokenClient = vi.fn((config: GoogleTokenClientConfig) => {
  requestAccessToken.mockImplementation(() => deliver(config))
  return { requestAccessToken }
})

beforeEach(() => {
  localStorage.clear()
  vi.clearAllMocks()
  vi.spyOn(Date, "now").mockReturnValue(now)
  vi.spyOn(console, "error").mockImplementation(() => {})
  vi.spyOn(console, "log").mockImplementation(() => {})
  vi.stubEnv("NEXT_PUBLIC_GOOGLE_DRIVE_CLIENT_ID", "test-client.googleusercontent.com")
  vi.stubEnv("NEXT_PUBLIC_GOOGLE_DRIVE_API_KEY", "test-google-drive-key")
  vi.stubGlobal("google", {
    accounts: { oauth2: { initTokenClient, revoke: vi.fn() } },
  })
  vi.stubGlobal("gapi", {
    load: vi.fn((_libraries: string, callback: () => void) => callback()),
    client: { init: vi.fn(async () => {}), setToken: vi.fn() },
  })
})

afterEach(() => {
  localStorage.clear()
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  vi.unstubAllEnvs()
})

describe("Google Drive authorization responses", () => {
  it.each([
    { name: "numeric lifetime", expiresIn: 120, lifetime: 120000 },
    { name: "string lifetime", expiresIn: "180", lifetime: 180000 },
    { name: "default lifetime", expiresIn: undefined, lifetime: 3600000 },
  ])("stores a successful response with $name", async ({ expiresIn, lifetime }) => {
    deliver = config => config.callback({ access_token: token, expires_in: expiresIn })
    const { result } = renderHook(() => useGoogleDriveAuth())
    await waitFor(() => expect(result.current.isLoading).toBe(false))

    await act(async () => {
      expect(await result.current.authenticate()).toBe(true)
    })

    expect(initTokenClient).toHaveBeenCalledOnce()
    expect(initTokenClient.mock.calls[0]?.[0]).toMatchObject({
      client_id: "test-client.googleusercontent.com",
      scope: "https://www.googleapis.com/auth/drive.file",
    })
    expect(requestAccessToken).toHaveBeenCalledExactlyOnceWith({ prompt: "consent" })
    expect(localStorage.getItem("block-content-editor_google_drive_token")).toBe(token)
    expect(localStorage.getItem("block-content-editor_google_drive_token_expiry")).toBe(String(now + lifetime))
    expect(localStorage.getItem("block-content-editor_google_drive_auth_time")).toBe(String(now))
    expect(result.current).toMatchObject({ isAuthenticated: true, accessToken: token, isLoading: false, error: null })
    expect(toast.error).not.toHaveBeenCalled()
  })

  it.each([
    { code: "access_denied", message: "Acesso negado pelo usuário" },
    { code: "popup_closed_by_user", message: "Autenticação cancelada pelo usuário" },
    { code: "", message: "Falha na autenticação" },
  ])("rejects an OAuth error with code '$code'", async ({ code, message }) => {
    deliver = config => config.callback({ error: code })
    const { result } = renderHook(() => useGoogleDriveAuth())
    await waitFor(() => expect(result.current.isLoading).toBe(false))

    await act(async () => {
      expect(await result.current.authenticate()).toBe(false)
    })

    expect(requestAccessToken).toHaveBeenCalledOnce()
    expect(result.current).toMatchObject({ isAuthenticated: false, accessToken: null, isLoading: false, error: message })
    expect(toast.error).toHaveBeenCalledExactlyOnceWith(message)
    expect(localStorage.getItem("block-content-editor_google_drive_token")).toBeNull()
    expect(localStorage.getItem("block-content-editor_google_drive_token_expiry")).toBeNull()
    expect(toast.success).not.toHaveBeenCalled()
  })

  it.each([
    { type: "popup_closed", message: "Autenticação cancelada pelo usuário" },
    { type: "popup_failed_to_open", message: "Falha na autenticação" },
    { type: "unknown", message: "Falha na autenticação" },
  ])("settles a $type SDK error and clears loading", async ({ type, message }) => {
    deliver = config => config.error_callback?.({ type })
    const { result } = renderHook(() => useGoogleDriveAuth())
    await waitFor(() => expect(result.current.isLoading).toBe(false))

    let outcome: Promise<boolean> | undefined
    act(() => { outcome = result.current.authenticate() })
    // Keep this assertion outside the hook's catch path: a synchronous exception
    // must not substitute for the SDK delivering its non-OAuth error callback.
    expect(initTokenClient).toHaveBeenCalledOnce()
    expect(initTokenClient.mock.calls[0]?.[0].error_callback).toBeTypeOf("function")
    expect(requestAccessToken).toHaveBeenCalledOnce()
    await act(async () => { expect(await outcome).toBe(false) })

    expect(result.current).toMatchObject({ isAuthenticated: false, accessToken: null, isLoading: false, error: message })
    expect(toast.error).toHaveBeenCalledExactlyOnceWith(message)
    expect(localStorage.getItem("block-content-editor_google_drive_token")).toBeNull()
    expect(toast.success).not.toHaveBeenCalled()
  })

  it("settles synchronous SDK initialization failures without storing a token", async () => {
    initTokenClient.mockImplementationOnce(() => { throw new Error("synthetic initialization failure") })
    const { result } = renderHook(() => useGoogleDriveAuth())
    await waitFor(() => expect(result.current.isLoading).toBe(false))

    await act(async () => {
      expect(await result.current.authenticate()).toBe(false)
    })

    expect(initTokenClient).toHaveBeenCalledOnce()
    expect(requestAccessToken).not.toHaveBeenCalled()
    expect(result.current).toMatchObject({ isAuthenticated: false, accessToken: null, isLoading: false, error: "Falha na autenticação" })
    expect(localStorage.getItem("block-content-editor_google_drive_token")).toBeNull()
    expect(toast.error).toHaveBeenCalledExactlyOnceWith("Falha na autenticação")
  })
})
