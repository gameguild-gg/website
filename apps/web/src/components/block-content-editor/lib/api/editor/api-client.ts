import { assertSafeServiceUrl } from "@/lib/security/safe-remote-url"
import { syncConfig } from "../../sync/editor/sync-config"
import type { ProjectData, ProjectMetadataRecord } from "../../storage/editor/project-data"

interface SyncResponse {
  success: boolean
  message?: string
  data?: any
}

interface TagData {
  id: string
  name: string
  projectIds: string[]
}

interface ApiConfig {
  baseUrl: string
  timeout: number
  retries: number
}

export class ApiClient {
  private config: ApiConfig
  private isOnline: boolean = typeof window !== "undefined" ? navigator.onLine : true

  constructor(config: Partial<ApiConfig> = {}) {
    const syncConfigData = syncConfig.getConfig()

    this.config = {
      baseUrl: syncConfigData.serverUrl,
      timeout: syncConfigData.timeout,
      retries: syncConfigData.retries,
      ...config,
    }

    if (typeof window !== "undefined") {
      // Monitor online status
      window.addEventListener("online", () => {
        this.isOnline = true
        if (syncConfigData.debugMode) {
          console.log("API Client: Back online")
        }
      })

      window.addEventListener("offline", () => {
        this.isOnline = false
        if (syncConfigData.debugMode) {
          console.log("API Client: Gone offline")
        }
      })
    }

    // Listen for config changes
    syncConfig.onConfigChange((newConfig) => {
      this.config.baseUrl = newConfig.serverUrl
      this.config.timeout = newConfig.timeout
      this.config.retries = newConfig.retries

      if (newConfig.debugMode) {
        console.log("API Client config updated:", this.config)
      }
    })
  }

  private async fetchWithRetry(
    url: string,
    options: RequestInit = {},
    retries: number = this.config.retries,
  ): Promise<Response> {
    const syncConfigData = syncConfig.getConfig()

    if (!syncConfig.isEnabled()) {
      throw new Error("Sync is disabled")
    }

    if (!this.isOnline) {
      throw new Error("No internet connection")
    }

    const controller = new AbortController()
    const timeoutId = setTimeout(() => controller.abort(), this.config.timeout)

    try {
      const response = await fetch(assertSafeServiceUrl(`${this.config.baseUrl}${url}`, this.config.baseUrl), {
        ...options,
        redirect: "error",
        signal: controller.signal,
        headers: {
          "Content-Type": "application/json",
          ...options.headers,
        },
      })

      clearTimeout(timeoutId)

      if (!response.ok) {
        // Check if response has content before trying to parse JSON
        let errorMessage = `HTTP ${response.status}: ${response.statusText}`

        try {
          const contentType = response.headers.get("content-type")
          if (contentType && contentType.includes("application/json")) {
            const errorData = await response.json()
            errorMessage = errorData.message || errorMessage
          }
        } catch (parseError) {
          // Ignore JSON parse errors for error responses
          if (syncConfigData.debugMode) {
            console.warn("Failed to parse error response as JSON:", parseError)
          }
        }

        throw new Error(errorMessage)
      }

      return response
    } catch (error) {
      clearTimeout(timeoutId)

      if (
        retries > 0 &&
        typeof error === "object" &&
        error !== null &&
        "name" in error &&
        (error as { name?: string }).name !== "AbortError"
      ) {
        if (syncConfigData.debugMode) {
          console.log(`API retry ${this.config.retries - retries + 1}/${this.config.retries} for ${url}`)
        }
        await new Promise((resolve) => setTimeout(resolve, 1000 * (this.config.retries - retries + 1)))
        return this.fetchWithRetry(url, options, retries - 1)
      }

      throw error
    }
  }

  async getProjectsMetadata(): Promise<ProjectMetadataRecord[]> {
    if (!syncConfig.isEnabled()) {
      return []
    }

    try {
      const response = await this.fetchWithRetry("/projects/metadata")

      // Check if response has content
      const contentType = response.headers.get("content-type")
      if (!contentType || !contentType.includes("application/json")) {
        throw new Error("Server returned non-JSON response")
      }

      const text = await response.text()
      if (!text.trim()) {
        throw new Error("Server returned empty response")
      }

      const data = JSON.parse(text)
      return data.projects || []
    } catch (error) {
      const syncConfigData = syncConfig.getConfig()

      if (syncConfigData.debugMode) {
        console.warn(
          "⚠️ Sync Warning: Failed to fetch projects metadata:",
          (error instanceof Error ? error.message : String(error))
        )
      } else {
        console.warn("Sync: Server unavailable for metadata fetch")
      }

      return []
    }
  }

  async getProject(id: string): Promise<ProjectData | null> {
    if (!syncConfig.isEnabled()) {
      return null
    }

    try {
      const response = await this.fetchWithRetry(`/projects/${id}`)

      const contentType = response.headers.get("content-type")
      if (!contentType || !contentType.includes("application/json")) {
        throw new Error("Server returned non-JSON response")
      }

      const text = await response.text()
      if (!text.trim()) {
        throw new Error("Server returned empty response")
      }

      const data = JSON.parse(text)
      return data.project || null
    } catch (error) {
      const syncConfigData = syncConfig.getConfig()

      if (syncConfigData.debugMode) {
        console.warn(
          `⚠️ Sync Warning: Failed to fetch project ${id}:`,
          error instanceof Error ? error.message : String(error)
        )
      }

      return null
    }
  }

  async createProject(project: ProjectData): Promise<SyncResponse> {
    if (!syncConfig.isEnabled()) {
      return { success: false, message: "Sync is disabled" }
    }

    try {
      const response = await this.fetchWithRetry("/projects", {
        method: "POST",
        body: JSON.stringify(project),
      })

      const data = await response.json()
      return { success: true, data }
    } catch (error) {
      const syncConfigData = syncConfig.getConfig()

      if (syncConfigData.debugMode) {
        console.warn(
          "⚠️ Sync Warning: Failed to create project:",
          error instanceof Error ? error.message : String(error)
        )
      }

      return { success: false, message: error instanceof Error ? error.message : String(error) }
    }
  }

  async updateProject(project: ProjectData): Promise<SyncResponse> {
    if (!syncConfig.isEnabled()) {
      return { success: false, message: "Sync is disabled" }
    }

    try {
      const response = await this.fetchWithRetry(`/projects/${project.id}`, {
        method: "PUT",
        body: JSON.stringify(project),
      })

      const data = await response.json()
      return { success: true, data }
    } catch (error) {
      const syncConfigData = syncConfig.getConfig()

      if (syncConfigData.debugMode) {
        console.warn(
          "⚠️ Sync Warning: Failed to update project:",
          error instanceof Error ? error.message : String(error)
        )
      }

      return { success: false, message: error instanceof Error ? error.message : String(error) }
    }
  }

  async syncTags(tags: Omit<TagData, "projectIds">[]): Promise<SyncResponse> {
    if (!syncConfig.isEnabled()) {
      return { success: false, message: "Sync is disabled" }
    }

    try {
      const response = await this.fetchWithRetry("/tags/sync", {
        method: "POST",
        body: JSON.stringify({ tags }),
      })

      const data = await response.json()
      return { success: true, data }
    } catch (error) {
      const syncConfigData = syncConfig.getConfig()

      if (syncConfigData.debugMode) {
        console.warn(
          "⚠️ Sync Warning: Failed to sync tags:",
          error instanceof Error ? error.message : String(error),
        )
      }

      return { success: false, message: error instanceof Error ? error.message : String(error) }
    }
  }

  async deleteProject(id: string): Promise<SyncResponse> {
    if (!syncConfig.isEnabled()) {
      return { success: false, message: "Sync is disabled" }
    }

    try {
      const response = await this.fetchWithRetry(`/projects/${id}`, {
        method: "DELETE",
      })

      const data = await response.json()
      return { success: true, data }
    } catch (error) {
      const syncConfigData = syncConfig.getConfig()

      if (syncConfigData.debugMode) {
        console.warn(
          "⚠️ Sync Warning: Failed to delete project:",
          error instanceof Error ? error.message : String(error)
        )
      }

      return { success: false, message: error instanceof Error ? error.message : String(error) }
    }
  }

  async checkProjectHash(id: string, localHash: string): Promise<{ needsUpdate: boolean; serverHash?: string }> {
    if (!syncConfig.isEnabled()) {
      return { needsUpdate: false }
    }

    try {
      const response = await this.fetchWithRetry(`/projects/${id}/hash`)
      const data = await response.json()

      return {
        needsUpdate: data.hash !== localHash,
        serverHash: data.hash,
      }
    } catch (error) {
      const syncConfigData = syncConfig.getConfig()

      if (syncConfigData.debugMode) {
        console.warn(
          `⚠️ Sync Warning: Failed to check hash for project ${id}:`,
          error instanceof Error ? error.message : String(error)
        )
      }

      return { needsUpdate: false }
    }
  }

  isConnected(): boolean {
    return this.isOnline && syncConfig.isEnabled()
  }
}
