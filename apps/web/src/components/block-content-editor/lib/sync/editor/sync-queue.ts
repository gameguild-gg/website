import { syncConfig } from "./sync-config"
import { ApiClient } from "../../api/editor/api-client"
import type { SyncQueueStats as QueueStats } from "./sync-types"
import type { ProjectData } from "../../storage/editor/project-data"

interface SyncQueueState {
  id: string
  projectId?: string
  timestamp: number
  retries: number
  status: "pending" | "processing" | "completed" | "failed"
  error?: string
  lastAttempt?: number
}

type SyncQueueOperation =
  | { type: "create" | "update"; data?: ProjectData }
  | { type: "delete"; data?: unknown }
  | { type: "tags_update"; data?: Parameters<ApiClient["syncTags"]>[0] }

type SyncQueueItem = SyncQueueState & SyncQueueOperation
type NewSyncQueueItem = Omit<SyncQueueState, "id" | "timestamp" | "retries" | "status"> & SyncQueueOperation

export class SyncQueue {
  private db: IDBDatabase | null = null
  private readonly DB_NAME = "GGEditorSyncDB"
  private readonly DB_VERSION = 1
  private readonly STORE_NAME = "sync_queue"
  private isInitialized = false
  private processingInterval: NodeJS.Timeout | null = null
  private apiClient: ApiClient

  constructor(apiClient: ApiClient) {
    this.apiClient = apiClient
  }

  async init(): Promise<void> {
    if (this.isInitialized) return

    await this.initIndexedDB()
    this.isInitialized = true

    // Start processing queue if sync is enabled
    if (syncConfig.isEnabled()) {
      this.startProcessing()
    }

    // Listen for config changes
    syncConfig.onConfigChange((config) => {
      if (config.enabled && !this.processingInterval) {
        this.startProcessing()
      } else if (!config.enabled && this.processingInterval) {
        this.stopProcessing()
      }
    })

    console.log("Sync Queue initialized")
  }

  private async initIndexedDB(): Promise<void> {
    return new Promise((resolve, reject) => {
      const request = indexedDB.open(this.DB_NAME, this.DB_VERSION)

      request.onerror = () => reject(request.error)
      request.onsuccess = () => {
        this.db = request.result
        resolve()
      }

      request.onupgradeneeded = (event) => {
        const db = (event.target as IDBOpenDBRequest).result

        if (!db.objectStoreNames.contains(this.STORE_NAME)) {
          const store = db.createObjectStore(this.STORE_NAME, { keyPath: "id" })
          store.createIndex("status", "status", { unique: false })
          store.createIndex("type", "type", { unique: false })
          store.createIndex("timestamp", "timestamp", { unique: false })
          store.createIndex("projectId", "projectId", { unique: false })
        }
      }
    })
  }

  async enqueue(item: NewSyncQueueItem): Promise<void> {
    if (!syncConfig.isEnabled()) {
      if (syncConfig.getConfig().debugMode) {
        console.log("Sync disabled, skipping queue item:", item)
      }
      return
    }

    if (!this.isInitialized) {
      throw new Error("Sync queue not initialized")
    }

    const queueItem: SyncQueueItem = {
      ...item,
      id: `${item.type}_${item.projectId}_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
      timestamp: Date.now(),
      retries: 0,
      status: "pending",
    }

    await this.saveItem(queueItem)

    if (syncConfig.getConfig().debugMode) {
      console.log("Enqueued sync item:", queueItem)
    }
  }

  private async saveItem(item: SyncQueueItem): Promise<void> {
    if (!this.db) throw new Error("IndexedDB not initialized")

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([this.STORE_NAME], "readwrite")
      const store = transaction.objectStore(this.STORE_NAME)
      const request = store.put(item)

      request.onsuccess = () => resolve()
      request.onerror = () => reject(request.error)
    })
  }

  async getStats(): Promise<QueueStats> {
    if (!this.isInitialized) {
      return { pending: 0, processing: 0, completed: 0, failed: 0, total: 0 }
    }

    const items = await this.getAllItems()
    const stats: QueueStats = {
      pending: 0,
      processing: 0,
      completed: 0,
      failed: 0,
      total: items.length,
    }

    items.forEach((item) => {
      stats[item.status]++
    })

    return stats
  }

  private async getAllItems(): Promise<SyncQueueItem[]> {
    if (!this.db) return []

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([this.STORE_NAME], "readonly")
      const store = transaction.objectStore(this.STORE_NAME)
      const request = store.getAll()

      request.onsuccess = () => resolve(request.result || [])
      request.onerror = () => reject(request.error)
    })
  }

  async getPendingItems(): Promise<SyncQueueItem[]> {
    if (!this.db) return []

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([this.STORE_NAME], "readonly")
      const store = transaction.objectStore(this.STORE_NAME)
      const index = store.index("status")
      const request = index.getAll("pending")

      request.onsuccess = () => {
        const items = request.result || []
        // Sort by timestamp (oldest first)
        items.sort((a, b) => a.timestamp - b.timestamp)
        resolve(items)
      }
      request.onerror = () => reject(request.error)
    })
  }

  async markAsProcessing(id: string): Promise<void> {
    const item = await this.getItem(id)
    if (item) {
      item.status = "processing"
      item.lastAttempt = Date.now()
      await this.saveItem(item)
    }
  }

  async markAsCompleted(id: string): Promise<void> {
    const item = await this.getItem(id)
    if (item) {
      item.status = "completed"
      await this.saveItem(item)
    }
  }

  async markAsFailed(id: string, error: string): Promise<void> {
    const item = await this.getItem(id)
    if (item) {
      item.status = "failed"
      item.error = error
      item.retries++
      await this.saveItem(item)
    }
  }

  private async getItem(id: string): Promise<SyncQueueItem | null> {
    if (!this.db) return null

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([this.STORE_NAME], "readonly")
      const store = transaction.objectStore(this.STORE_NAME)
      const request = store.get(id)

      request.onsuccess = () => resolve(request.result || null)
      request.onerror = () => reject(request.error)
    })
  }

  async retryFailedItems(): Promise<void> {
    if (!this.db) return

    const transaction = this.db.transaction([this.STORE_NAME], "readwrite")
    const store = transaction.objectStore(this.STORE_NAME)
    const index = store.index("status")
    const request = index.getAll("failed")

    request.onsuccess = () => {
      const failedItems = request.result || []
      const config = syncConfig.getConfig()

      failedItems.forEach((item) => {
        if (item.retries < config.retries) {
          item.status = "pending"
          item.error = undefined
          store.put(item)
        }
      })
    }
  }

  async clearCompleted(): Promise<void> {
    if (!this.db) return

    const transaction = this.db.transaction([this.STORE_NAME], "readwrite")
    const store = transaction.objectStore(this.STORE_NAME)
    const index = store.index("status")
    const request = index.getAll("completed")

    request.onsuccess = () => {
      const completedItems = request.result || []
      const cutoff = Date.now() - 24 * 60 * 60 * 1000 // 24 hours ago

      completedItems.forEach((item) => {
        if (item.timestamp < cutoff) {
          store.delete(item.id)
        }
      })
    }
  }

  private startProcessing(): void {
    if (this.processingInterval) return

    const config = syncConfig.getConfig()
    this.processingInterval = setInterval(() => {
      this.processQueue().catch((error) => {
        if (config.debugMode) {
          console.error("Error processing sync queue:", error)
        }
      })
    }, config.syncInterval)

    if (config.debugMode) {
      console.log("Started sync queue processing")
    }
  }

  private stopProcessing(): void {
    if (this.processingInterval) {
      clearInterval(this.processingInterval)
      this.processingInterval = null

      if (syncConfig.getConfig().debugMode) {
        console.log("Stopped sync queue processing")
      }
    }
  }

  private async processQueue(): Promise<void> {
    if (!syncConfig.isEnabled() || !navigator.onLine) {
      return
    }

    const pendingItems = await this.getPendingItems()
    const config = syncConfig.getConfig()
    const batchSize = Math.min(pendingItems.length, config.batchSize)

    if (batchSize === 0) return

    const batch = pendingItems.slice(0, batchSize)

    if (config.debugMode) {
      console.log(`Processing ${batch.length} sync items`)
    }

    for (const item of batch) {
      try {
        await this.markAsProcessing(item.id)

        let response
        const operationType = item.type
        switch (item.type) {
          case "create":
            if (!item.data) throw new Error("Missing data for create operation")
            // Assuming createProject is on apiClient
            response = await this.apiClient.createProject(item.data)
            break
          case "update":
            if (!item.data) throw new Error("Missing data for update operation")
            response = await this.apiClient.updateProject(item.data)
            break
          case "delete":
            if (!item.projectId) throw new Error("Missing projectId for delete operation")
            response = await this.apiClient.deleteProject(item.projectId)
            break
          case "tags_update":
            if (!item.data) throw new Error("Missing data for tags_update operation")
            response = await this.apiClient.syncTags(item.data)
            break
          default:
            throw new Error(`Unknown sync item type: ${operationType}`)
        }

        if (response.success) {
          await this.markAsCompleted(item.id)
        } else {
          throw new Error(response.message || "API operation failed")
        }
      } catch (error) {
        let errorMessage = "Unknown error"
        if (error instanceof Error) {
          errorMessage = error.message
        } else if (typeof error === "string") {
          errorMessage = error
        }
        await this.markAsFailed(item.id, errorMessage)
      }
    }

    // Clean up old completed items
    await this.clearCompleted()
  }

  destroy(): void {
    this.stopProcessing()
    if (this.db) {
      this.db.close()
      this.db = null
    }
    this.isInitialized = false
  }
}
