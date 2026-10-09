/**
 * Helpers shared between the HTML editor surface and the HTML preview
 * surface. Lives outside of `html-editor.tsx` so the read-only preview
 * (used in the viewer / serialized renderer) does not pull in the Monaco
 * bundle.
 *
 * Security model:
 *   - JS-bearing extensions are rejected at file create / rename time.
 *   - Inline `<script>` tags are stripped from the preview render.
 *   - DOMPurify sanitises the index.html before parsing, with
 *     `WHOLE_DOCUMENT` so we keep <head>, <link>, <style>, <meta>.
 *   - The preview iframe runs with an empty `sandbox` attribute (no
 *     scripts, no same-origin), so even if a tag slipped through it
 *     could not execute.
 *
 * Cross-file resolution:
 *   - `<link rel="stylesheet" href="X.css">` is inlined as a `<style>`
 *     block with the CSS file's content.
 *   - `[src]` / anchor `[href]` referencing a known file is replaced
 *     with a `data:` URL of that file's content. We use data URLs (not
 *     blob URLs) so callers never need to manage URL lifecycle.
 */

import type { HTMLData, HTMLFile } from "../../nodes/html-node"
import type { SupportedLanguage } from "../code-studio/types"

// ---------------------------------------------------------------------------
// Allowed / blocked file extensions
// ---------------------------------------------------------------------------

/**
 * Lowercase extensions accepted by the HTML custom-node editor. Anything
 * outside this list — and notably every JS-flavoured extension — is
 * rejected at the editor boundary.
 */
export const ALLOWED_HTML_EXTENSIONS = [
  "html",
  "htm",
  "css",
  "json",
  "xml",
  "svg",
  "md",
] as const

export type AllowedHTMLExtension = (typeof ALLOWED_HTML_EXTENSIONS)[number]

/**
 * Extensions explicitly rejected. Listed separately so the error message
 * can say "scripts blocked" instead of "unknown extension" — better UX
 * for the common attempt to add a .js file.
 */
export const BLOCKED_HTML_EXTENSIONS = [
  "js",
  "mjs",
  "cjs",
  "ts",
  "tsx",
  "jsx",
  "wasm",
  "py",
  "rb",
  "php",
  "sh",
  "bat",
  "exe",
  "dll",
] as const

export function getExtension(name: string): string {
  const trimmed = name.trim()
  const dot = trimmed.lastIndexOf(".")
  if (dot <= 0 || dot === trimmed.length - 1) return ""
  return trimmed.slice(dot + 1).toLowerCase()
}

export interface FileNameValidation {
  ok: boolean
  reason?: string
}

export function validateHTMLFileName(name: string, existingNames: string[] = []): FileNameValidation {
  const trimmed = name.trim()
  if (!trimmed) return { ok: false, reason: "File name is required" }
  if (/[\\/]/.test(trimmed)) return { ok: false, reason: "File name cannot contain / or \\" }
  if (trimmed.length > 100) return { ok: false, reason: "File name is too long" }
  const ext = getExtension(trimmed)
  if (!ext) return { ok: false, reason: "File must have an extension" }
  if ((BLOCKED_HTML_EXTENSIONS as readonly string[]).includes(ext)) {
    return { ok: false, reason: `.${ext} is not allowed (scripts are blocked)` }
  }
  if (!(ALLOWED_HTML_EXTENSIONS as readonly string[]).includes(ext)) {
    return { ok: false, reason: `.${ext} is not supported (use ${ALLOWED_HTML_EXTENSIONS.join(", ")})` }
  }
  const lower = trimmed.toLowerCase()
  if (existingNames.some(n => n.toLowerCase() === lower)) {
    return { ok: false, reason: `A file named "${trimmed}" already exists` }
  }
  return { ok: true }
}

// ---------------------------------------------------------------------------
// Language inference for the Monaco surface
// ---------------------------------------------------------------------------

export function languageForFile(name: string): SupportedLanguage {
  switch (getExtension(name)) {
    case "html":
    case "htm":
    case "svg":
      return "html"
    case "css":
      return "css"
    case "json":
      return "json"
    case "xml":
      return "xml"
    case "md":
      return "markdown"
    default:
      return "html"
  }
}

// ---------------------------------------------------------------------------
// MIME helpers (for cross-file data: URLs in the preview)
// ---------------------------------------------------------------------------

export function mimeForFile(name: string): string {
  switch (getExtension(name)) {
    case "html":
    case "htm":
      return "text/html;charset=utf-8"
    case "css":
      return "text/css;charset=utf-8"
    case "json":
      return "application/json;charset=utf-8"
    case "xml":
      return "application/xml;charset=utf-8"
    case "svg":
      return "image/svg+xml;charset=utf-8"
    case "md":
      return "text/markdown;charset=utf-8"
    default:
      return "text/plain;charset=utf-8"
  }
}

/** UTF-8 safe base64 encode (`btoa` is latin-1 only). */
function encodeBase64Utf8(text: string): string {
  if (typeof window === "undefined") return ""
  const bytes = new TextEncoder().encode(text)
  let binary = ""
  for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]!)
  return window.btoa(binary)
}

export function dataUrlForFile(file: HTMLFile): string {
  return `data:${mimeForFile(file.name)};base64,${encodeBase64Utf8(file.content)}`
}

// ---------------------------------------------------------------------------
// Preview document builder
// ---------------------------------------------------------------------------

const PREVIEW_PLACEHOLDER = `<!DOCTYPE html><html><head><meta charset="utf-8"></head><body style="font-family:system-ui,sans-serif;color:#9ca3af;padding:24px"><em>No index.html — create one to see the preview.</em></body></html>`

function findFile(files: HTMLFile[], name: string): HTMLFile | undefined {
  const lower = name.toLowerCase()
  return files.find(f => f.name.toLowerCase() === lower)
}

/**
 * Build the `srcdoc` for the preview iframe by resolving cross-file
 * references inside `index.html` against `files`. Returns a placeholder
 * document when no `index.html` is present.
 *
 * Must run client-side (uses DOMParser). The DOMPurify import is
 * client-only as well.
 */
export function buildHTMLPreviewSrcDoc(files: HTMLFile[]): string {
  if (typeof window === "undefined") return PREVIEW_PLACEHOLDER
  const index = findFile(files, "index.html")
  if (!index) return PREVIEW_PLACEHOLDER

  // Lazy require so server bundles don't pull DOMPurify.
  // dompurify v3 exports the factory as the module itself (callable);
  // invoke it with `window` to obtain an instance bound to this realm.
  const createDOMPurify = require("dompurify") as (win: Window) => {
    sanitize: (input: string, cfg?: Record<string, unknown>) => string
  }
  const DOMPurify = createDOMPurify(window)

  // Strip <script> tags up-front (belt-and-suspenders alongside the sandbox
  // attribute and DOMPurify's FORBID_TAGS). DOMParser-based removal because
  // a regex strip is bypassable by nested/trick markup.
  const parsed = new DOMParser().parseFromString(index.content, "text/html")
  parsed.querySelectorAll("script").forEach(el => el.remove())
  const stripped = `<!DOCTYPE html>${parsed.documentElement.outerHTML}`

  const sanitized = DOMPurify.sanitize(stripped, {
    WHOLE_DOCUMENT: true,
    FORBID_TAGS: ["script"],
    FORBID_ATTR: [
      "onload",
      "onerror",
      "onclick",
      "onmouseover",
      "onfocus",
      "onblur",
      "onchange",
      "oninput",
      "onsubmit",
      "onkeydown",
      "onkeyup",
      "onkeypress",
    ],
  }) as string

  const doc = new DOMParser().parseFromString(sanitized, "text/html")

  // Inline referenced stylesheets.
  doc.querySelectorAll("link[rel='stylesheet'][href]").forEach(link => {
    const href = link.getAttribute("href") ?? ""
    const file = findFile(files, href)
    if (!file) return
    const style = doc.createElement("style")
    style.textContent = file.content
    link.replaceWith(style)
  })

  // Rewrite [src]/[href] referring to known sibling files into data: URLs.
  const rewriteAttr = (selector: string, attr: "src" | "href") => {
    doc.querySelectorAll(selector).forEach(el => {
      const v = el.getAttribute(attr)
      if (!v) return
      const file = findFile(files, v)
      if (file) el.setAttribute(attr, dataUrlForFile(file))
    })
  }
  rewriteAttr("[src]", "src")
  rewriteAttr("a[href]", "href")
  rewriteAttr("link[href]", "href")
  rewriteAttr("use[href]", "href")
  rewriteAttr("image[href]", "href")

  // Ensure <base target="_blank"> so anchor clicks don't try to navigate
  // the iframe (which has empty sandbox and would just break).
  if (!doc.querySelector("base")) {
    const base = doc.createElement("base")
    base.setAttribute("target", "_blank")
    doc.head.prepend(base)
  }

  return `<!DOCTYPE html>${doc.documentElement.outerHTML}`
}

// ---------------------------------------------------------------------------
// Default seed used when a new "html" block is created
// ---------------------------------------------------------------------------

export function createDefaultHTMLData(): HTMLData {
  const indexId = (typeof crypto !== "undefined" ? crypto.randomUUID() : `idx-${Date.now()}`)
  const cssId = (typeof crypto !== "undefined" ? crypto.randomUUID() : `css-${Date.now()}`)
  return {
    files: [
      {
        id: indexId,
        name: "index.html",
        content: `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Custom Block</title>
  <link rel="stylesheet" href="styles.css">
</head>
<body>
  <main>
    <h1>Hello, world!</h1>
    <p>Edit <code>index.html</code> on the left to start building.</p>
  </main>
</body>
</html>`,
      },
      {
        id: cssId,
        name: "styles.css",
        content: `body {
  font-family: system-ui, -apple-system, sans-serif;
  margin: 0;
  padding: 24px;
  color: #1f2937;
  background: #ffffff;
}

main { max-width: 640px; margin: 0 auto; }
h1 { color: #ea580c; }
code { background: #f3f4f6; padding: 2px 6px; border-radius: 4px; }
`,
      },
    ],
    openTabs: [indexId],
    activeFileId: indexId,
  }
}
