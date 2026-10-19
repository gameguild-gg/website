import { GoogleDriveService } from "../../../services/editor/google-drive-service"
import type { ProjectData } from "../../storage/editor/project-data"

interface SyncResult {
  success: boolean
  error?: string
  projectId?: string
}

export class GoogleDriveSync {
  private googleDriveService: GoogleDriveService

  constructor() {
    this.googleDriveService = GoogleDriveService.getInstance()
  }

  /**
   * Sync a project to Google Drive
   */
  async syncToGoogleDrive(project: ProjectData): Promise<SyncResult> {
    try {
      console.log("GoogleDriveSync: Starting sync for project:", project.name)
      
      if (!project.storageType || project.storageType !== "google-drive") {
        console.log("GoogleDriveSync: Project not configured for Google Drive storage")
        return {
          success: false,
          error: "Project is not configured for Google Drive storage"
        }
      }

      console.log("GoogleDriveService isReady:", this.googleDriveService.isReady())
      console.log("GoogleDriveService instance:", this.googleDriveService)
      
      if (!this.googleDriveService.isReady()) {
        console.error("GoogleDriveSync: Service not ready")
        return {
          success: false,
          error: "Google Drive service is not authenticated or ready"
        }
      }

      console.log("GoogleDriveSync: Calling saveProject...")
      await this.googleDriveService.saveProject(
        project.id,
        project.name,
        project.data,
        project.tags
      )

      console.log("GoogleDriveSync: Project saved successfully")
      return {
        success: true,
        projectId: project.id
      }
    } catch (error) {
      console.error("Google Drive sync error:", error)
      return {
        success: false,
        error: error instanceof Error ? error.message : "Unknown error"
      }
    }
  }

  /**
   * Load a project from Google Drive
   */
  async loadFromGoogleDrive(projectId: string): Promise<ProjectData | null> {
    try {
      if (!this.googleDriveService.isReady()) {
        console.error("Google Drive service not ready")
        return null
      }

      const project = await this.googleDriveService.getProject(projectId)
      
      if (project) {
        return {
          id: project.id,
          name: project.name,
          data: project.data,
          tags: project.tags,
          metadata: {
            size: project.size,
            hash: project.hash,
            createdAt: project.createdAt,
            updatedAt: project.updatedAt,
          },
          storageType: "google-drive"
        }
      }
      
      return null
    } catch (error) {
      console.error("Google Drive load error:", error)
      return null
    }
  }

  /**
   * List all projects from Google Drive
   */
  async listFromGoogleDrive(): Promise<ProjectData[]> {
    try {
      if (!this.googleDriveService.isReady()) {
        return []
      }

      const projects = await this.googleDriveService.listProjects()
      
      return projects.map((project) => ({
        id: project.id,
        name: project.name,
        data: project.data,
        tags: project.tags,
        metadata: {
          size: project.size,
          hash: project.hash,
          createdAt: project.createdAt,
          updatedAt: project.updatedAt,
        },
        storageType: "google-drive" as const
      }))
    } catch (error) {
      console.error("Google Drive list error:", error)
      return []
    }
  }

  /**
   * Delete a project from Google Drive
   */
  async deleteFromGoogleDrive(projectId: string): Promise<SyncResult> {
    try {
      if (!this.googleDriveService.isReady()) {
        return {
          success: false,
          error: "Google Drive service is not authenticated or ready"
        }
      }

      await this.googleDriveService.deleteProject(projectId)
      
      return {
        success: true,
        projectId
      }
    } catch (error) {
      console.error("Google Drive delete error:", error)
      return {
        success: false,
        error: error instanceof Error ? error.message : "Unknown error"
      }
    }
  }

  /**
   * Check if Google Drive is available and authenticated
   */
  async isGoogleDriveAvailable(): Promise<boolean> {
    try {
      return this.googleDriveService.isReady()
    } catch (error) {
      console.error("Google Drive availability check error:", error)
      return false
    }
  }

  /**
   * Sync all Google Drive projects to local storage
   */
  async syncAllFromGoogleDrive(): Promise<{
    synced: ProjectData[]
    errors: { projectId: string; error: string }[]
  }> {
    const synced: ProjectData[] = []
    const errors: { projectId: string; error: string }[] = []

    try {
      const projects = await this.listFromGoogleDrive()
      
      for (const project of projects) {
        try {
          const fullProject = await this.loadFromGoogleDrive(project.id)
          if (fullProject) {
            synced.push(fullProject)
          } else {
            errors.push({
              projectId: project.id,
              error: "Failed to load project data"
            })
          }
        } catch (error) {
          errors.push({
            projectId: project.id,
            error: error instanceof Error ? error.message : "Unknown error"
          })
        }
      }
    } catch (error) {
      console.error("Google Drive sync all error:", error)
    }

    return { synced, errors }
  }
}
