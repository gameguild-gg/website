"use client"

import { useCallback, useEffect, useRef, useState } from "react"

/**
 * Minimal ambient types for the Google Identity Services surface used here.
 * Avoids pulling in @types/google.accounts as a new dependency.
 */
export interface GisCredentialResponse {
  credential: string
  select_by?: string
}

export interface GisIdConfiguration {
  client_id: string
  callback: (response: GisCredentialResponse) => void
  auto_select?: boolean
  cancel_on_tap_outside?: boolean
}

export type GisButtonType = "standard" | "icon"
export type GisButtonTheme = "outline" | "filled_blue" | "filled_black"
export type GisButtonSize = "large" | "medium" | "small"
export type GisButtonShape = "rectangular" | "pill" | "circle" | "square"
export type GisButtonText =
  | "signin_with"
  | "signup_with"
  | "continue_with"
  | "signin"

export interface GisRenderButtonOptions {
  type?: GisButtonType
  theme?: GisButtonTheme
  size?: GisButtonSize
  text?: GisButtonText
  shape?: GisButtonShape
  width?: number
  locale?: string
}

export interface GisId {
  initialize: (config: GisIdConfiguration) => void
  renderButton: (parent: HTMLElement, options: GisRenderButtonOptions) => void
  prompt: () => void
  disableAutoSelect: () => void
  cancel: () => void
}

/**
 * Typed accessor for the identity surface shared with the editor's Drive SDK.
 */
function getGisId(): GisId | undefined {
  return window.google?.accounts?.id
}

/* ------------------------------------------------------------------ */
/*  Module-level singletons (one script, one initialize per page)     */
/* ------------------------------------------------------------------ */

const GIS_SCRIPT_SRC = "https://accounts.google.com/gsi/client"

// ponytail: module-level cache — GIS script must load once and initialize
// must run once per page even when multiple consumers mount. The One Tap
// component (T6) reuses this surface without re-initializing.
let scriptPromise: Promise<void> | null = null
let initialized = false

function loadGisScript(): Promise<void> {
  if (scriptPromise) return scriptPromise
  scriptPromise = new Promise<void>((resolve, reject) => {
    if (typeof window === "undefined" || typeof document === "undefined") {
      reject(new Error("Google Identity Services requires a browser environment"))
      return
    }
    // Already present (e.g. test pre-seed, or another tab instance).
    if (getGisId()) {
      resolve()
      return
    }
    const script = document.createElement("script")
    script.src = GIS_SCRIPT_SRC
    script.async = true
    script.defer = true
    script.onload = () => resolve()
    script.onerror = () => {
      // Allow a future consumer to retry after a transient failure.
      scriptPromise = null
      reject(new Error("Failed to load Google Identity Services script"))
    }
    document.head.appendChild(script)
  })
  return scriptPromise
}

/* ------------------------------------------------------------------ */
/*  Hook                                                               */
/* ------------------------------------------------------------------ */

export type GisStatus = "loading" | "ready" | "error"

export interface UseGoogleIdentityServiceOptions {
  /**
   * Invoked with the (untrusted) Google ID token. Callers route it to the
   * backend verifier via signIn("google", { idToken }). NEVER decode it
   * client-side.
   */
  onCredential: (credential: string) => void
}

export interface GoogleIdentityService {
  status: GisStatus
  /**
   * Renders Google's branded Sign-In button into `parent`. No-op until
   * status === "ready".
   */
  renderButton: (parent: HTMLElement, options: GisRenderButtonOptions) => void
  /**
   * Triggers the One Tap prompt. Reused by T6 (<GoogleOneTap/>).
   */
  prompt: () => void
}

/**
 * Test-only seam: resets the module-level singleton guards so each test
 * gets a clean initialize. NOT for production use.
 */
export function __resetGisForTest(): void {
  scriptPromise = null
  initialized = false
}

export function useGoogleIdentityService({
  onCredential,
}: UseGoogleIdentityServiceOptions): GoogleIdentityService {
  const [status, setStatus] = useState<GisStatus>("loading")
  // Latest callback in a ref so initialize (called once) always dispatches
  // to the freshest consumer without re-initializing.
  const callbackRef = useRef(onCredential)
  callbackRef.current = onCredential

  useEffect(() => {
    let alive = true
    const clientId = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID
    if (!clientId) {
      setStatus("error")
      return
    }

    loadGisScript()
      .then(() => {
        if (!alive) return
        const id = getGisId()
        if (!id) {
          setStatus("error")
          return
        }
        if (!initialized) {
          id.initialize({
            client_id: clientId,
            callback: ({ credential }) => {
              if (credential) callbackRef.current(credential)
            },
            // ponytail: no auto_select — it silently re-signs-in the previous
            // Google account on /sign-in and hijacks manual credential logins
            // (bug: admin login landing as old account -> dashboard 403).
            // One Tap prompt + button still work via explicit click.
            auto_select: false,
            cancel_on_tap_outside: false,
          })
          initialized = true
        }
        setStatus("ready")
      })
      .catch(() => {
        if (alive) setStatus("error")
      })

    return () => {
      alive = false
    }
  }, [])

  const renderButton = useCallback(
    (parent: HTMLElement, options: GisRenderButtonOptions) => {
      getGisId()?.renderButton(parent, options)
    },
    [],
  )

  const prompt = useCallback(() => {
    getGisId()?.prompt()
  }, [])

  return { status, renderButton, prompt }
}
