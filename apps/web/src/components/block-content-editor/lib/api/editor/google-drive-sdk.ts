import type { GisId } from "@/components/use-google-identity-service"

// The SDK surface used by the editor after the existing script loaders finish.
// These declarations do not validate responses or establish that scripts loaded.
export type GoogleDriveTokenResponse =
  | { error?: undefined; access_token: string; expires_in?: number | string }
  | { error: string; error_description?: string; error_uri?: string }

export interface GoogleTokenClientConfig {
  client_id: string
  scope: string
  callback: (response: GoogleDriveTokenResponse) => void
  error_callback?: (error: { type: string }) => void
}

export interface GoogleIdentityServices {
  accounts: {
    id?: GisId
    oauth2: {
      initTokenClient(config: GoogleTokenClientConfig): {
        requestAccessToken(config?: { prompt?: string }): void
      }
      revoke(accessToken: string, done: () => void): void
    }
  }
}

interface GoogleApiResponse<Result> {
  body: string
  result: Result
}

// A fields mask can omit any File property.
interface GoogleDriveFile {
  id?: string
  name?: string
  mimeType?: string
  createdTime?: string
  modifiedTime?: string
  size?: string
}

export interface GoogleDriveFolder {
  id: string
  name: string
  createdTime: string
}

interface GoogleDriveListOptions {
  q?: string
  fields?: string
  orderBy?: string
}

interface GoogleDriveFiles {
  list(options: GoogleDriveListOptions & { fields: "files(id, name, createdTime)" }): PromiseLike<GoogleApiResponse<{ files?: GoogleDriveFolder[] }>>
  list(options: GoogleDriveListOptions): PromiseLike<GoogleApiResponse<{ files?: GoogleDriveFile[] }>>
  create(options: {
    resource: { name: string; mimeType: string; parents?: string[] }
    fields?: string
  }): PromiseLike<GoogleApiResponse<GoogleDriveFile>>
  get(options: { fileId: string; alt?: "media" }): PromiseLike<GoogleApiResponse<unknown>>
  delete(options: { fileId: string }): PromiseLike<GoogleApiResponse<unknown>>
}

export interface GoogleDriveApi {
  load(libraries: string, callback: () => void): void
  client: {
    init(config: { apiKey?: string; discoveryDocs?: string[] }): PromiseLike<unknown>
    setToken(token: { access_token: string }): void
    drive: { files: GoogleDriveFiles }
    request<Result = unknown>(options: {
      path: string
      method?: string
      params?: Record<string, string | number | boolean>
      headers?: Record<string, string>
      body?: string | object
    }): PromiseLike<GoogleApiResponse<Result>>
  }
}
