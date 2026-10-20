"use client"

import { ProjectExporter, type ProjectData as ExportProjectData } from "../../lib/interopAdapter/project-exporter"
import { ProjectImporter, type FolderStructureData } from "../../lib/interopAdapter/project-importer"
import { HashManager } from "../../lib/sync/editor/hash-manager"
import {
  isGoogleDriveProjectContent,
  isGoogleDriveProjectMetadata,
  type GoogleDriveProjectMetadata,
} from "./google-drive-project-validation"

interface GoogleDriveFile {
  id: string
  name: string
  mimeType: string
  createdTime: string
  modifiedTime: string
  size?: string
}

interface GoogleDriveProjectData extends GoogleDriveProjectMetadata {
  data: string
  driveFileId?: string
  storageType: string
}

export class GoogleDriveService {
  private static instance: GoogleDriveService
  private folderId: string | null = null
  private accessToken: string | null = null
  private readyState: boolean | null = null // Cache for isReady state
  private lastReadyCheck: number = 0
  private readonly READY_CHECK_CACHE_MS = 5000 // Cache isReady for 5 seconds

  private constructor() {
    // Load saved folder ID
    if (typeof window !== 'undefined') {
      this.folderId = localStorage.getItem('block-content-editor_google_drive_folder')
    }
  }

  static getInstance(): GoogleDriveService {
    if (!GoogleDriveService.instance) {
      GoogleDriveService.instance = new GoogleDriveService()
    }
    return GoogleDriveService.instance
  }

  // Set authentication token and folder
  setAuth(accessToken: string, folderId: string) {
    this.accessToken = accessToken
    this.folderId = folderId
    this.readyState = null // Reset cache when auth changes
  }

  // Check if service is ready (with caching)
  isReady(): boolean {
    const now = Date.now()

    // Return cached result if within cache period
    if (this.readyState !== null && (now - this.lastReadyCheck) < this.READY_CHECK_CACHE_MS) {
      return this.readyState
    }

    const hasToken = !!this.accessToken
    const hasFolder = !!this.folderId
    const hasGapi = !!(window.gapi?.client?.drive)

    this.readyState = hasToken && hasFolder && hasGapi
    this.lastReadyCheck = now

    if (this.readyState) {
      console.log("GoogleDriveService ready:", {
        hasToken,
        hasFolder,
        hasGapi,
        folderId: this.folderId
      })
    }

    return this.readyState
  }

  // Set authentication token for GAPI client
  private setAuthToken() {
    if (this.accessToken && window.gapi?.client) {
      window.gapi.client.setToken({ access_token: this.accessToken })
    }
  }

  // Upload file content as JSON
  private async uploadJsonFile(
    fileName: string,
    content: object,
    fileId?: string
  ): Promise<string | null> {
    if (!this.isReady()) {
      throw new Error('Google Drive service not ready')
    }

    const jsonContent = JSON.stringify(content, null, 2)
    const metadata = {
      name: fileName,
      parents: fileId ? undefined : [this.folderId!],
      mimeType: 'application/json',
    }

    try {
      const form = new FormData()
      form.append('metadata', new Blob([JSON.stringify(metadata)], { type: 'application/json' }))
      form.append('file', new Blob([jsonContent], { type: 'application/json' }))

      const url = fileId
        ? `https://www.googleapis.com/upload/drive/v3/files/${fileId}?uploadType=multipart`
        : 'https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart'

      const method = fileId ? 'PATCH' : 'POST'

      const response = await fetch(url, {
        method,
        headers: {
          'Authorization': `Bearer ${this.accessToken}`,
        },
        body: form,
      })

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`)
      }

      const result = await response.json()
      return result.id
    } catch (error) {
      console.error('Failed to upload file:', error)
      throw new Error('Falha ao salvar arquivo no Google Drive')
    }
  }

  // Download file content
  private async downloadJsonFile(fileId: string): Promise<unknown> {
    if (!this.isReady()) {
      throw new Error('Google Drive service not ready')
    }

    try {
      // Set auth token before API call
      this.setAuthToken()

      const response = await window.gapi.client.drive.files.get({
        fileId: fileId,
        alt: 'media',
      })

      return JSON.parse(response.body)
    } catch (error) {
      console.error('Failed to download file:', error)
      return null
    }
  }

  // List projects using new folder structure
  async listProjects(): Promise<GoogleDriveProjectData[]> {
    if (!this.isReady()) {
      return []
    }

    try {
      // Set auth token before API call
      this.setAuthToken()

      // Find all project folders (projeto-*)
      const response = await window.gapi.client.drive.files.list({
        q: `'${this.folderId}' in parents and mimeType='application/vnd.google-apps.folder' and name contains 'projeto-' and trashed=false`,
        fields: 'files(id, name, createdTime, modifiedTime)',
        orderBy: 'modifiedTime desc',
      })

      const projectFolders = response.result.files || []
      const projects: GoogleDriveProjectData[] = []

      // For each project folder, get metadata from index.json
      for (const folder of projectFolders) {
        try {
          const indexFileResponse = await window.gapi.client.drive.files.list({
            q: `'${folder.id}' in parents and name='index.json' and trashed=false`,
            fields: 'files(id)',
          })

          const indexFile = indexFileResponse.result.files?.[0]
          if (indexFile) {
            const metadata = await this.downloadJsonFile(indexFile.id!)
            if (isGoogleDriveProjectMetadata(metadata)) {
              projects.push({
                id: metadata.id,
                name: metadata.name,
                data: "", // Placeholder - actual data loaded when opening project
                tags: metadata.tags || [],
                size: metadata.size || 0,
                createdAt: metadata.createdAt,
                updatedAt: metadata.updatedAt,
                hash: metadata.hash || "",
                driveFileId: folder.id!, // Use folder ID for reference
                storageType: "google-drive"
              })
            }
          }
        } catch (error) {
          console.error(`Failed to load project metadata from folder ${folder.name}:`, error)
        }
      }

      return projects
    } catch (error) {
      console.error('Failed to list projects:', error)
      return []
    }
  }

  // Save project to Google Drive using new folder structure
  async saveProject(
    id: string,
    name: string,
    data: string,
    tags: string[]
  ): Promise<void> {
    if (!this.isReady()) {
      throw new Error('Google Drive service not ready')
    }

    this.setAuthToken()

    const hash = await HashManager.generateHash(data)

    try {
      // 1. Prepare project data for export using ProjectExporter
      const projectData: ExportProjectData = {
        id,
        name,
        data,
        tags,
        size: new Blob([data]).size,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        hash,
        storageType: "google-drive"
      }

      const exportedProject = await ProjectExporter.prepareForExport(projectData, hash)
      if (exportedProject.assets.length > 0) {
        throw new Error(
          "Google Drive folder sync cannot carry browser-local assets yet. Download a ZIP bundle instead.",
        )
      }

      // 2. Create or find project folder
      let projectFolderId = await this.findProjectFolder(exportedProject.folderName)

      if (!projectFolderId) {
        projectFolderId = await this.createProjectFolder(exportedProject.folderName)
      }

      // 3. Save index.json (metadata) using ProjectExporter format
      await this.saveFileToFolder(
        projectFolderId,
        'index.json',
        JSON.stringify(exportedProject.metadata, null, 2),
        'application/json'
      )

      // 4. Save data file using ProjectExporter format
      await this.saveFileToFolder(
        projectFolderId,
        'data.block-content-editor',
        exportedProject.data,
        'application/json'
      )

      console.log(`Project ${id} saved successfully to Google Drive using ProjectExporter`)

    } catch (error) {
      console.error('Save project failed:', error)
      throw error
    }
  }

  // Helper: Find project folder by name
  private async findProjectFolder(folderName: string): Promise<string | null> {
    try {
      const response = await window.gapi.client.drive.files.list({
        q: `name='${folderName}' and mimeType='application/vnd.google-apps.folder' and parents in '${this.folderId}' and trashed=false`,
        fields: 'files(id, name)',
      })

      return response.result.files?.[0]?.id || null
    } catch (error) {
      console.error('Failed to find project folder:', error)
      return null
    }
  }

  // Helper: Create project folder
  private async createProjectFolder(folderName: string): Promise<string> {
    try {
      const response = await window.gapi.client.drive.files.create({
        resource: {
          name: folderName,
          mimeType: 'application/vnd.google-apps.folder',
          parents: [this.folderId!],
        },
        fields: 'id',
      })

      return response.result.id!
    } catch (error) {
      console.error('Failed to create project folder:', error)
      throw error
    }
  }

  // Helper: Save file to specific folder
  private async saveFileToFolder(
    folderId: string,
    fileName: string,
    content: string,
    mimeType: string
  ): Promise<string> {
    try {
      // Check if file already exists
      const existingFileResponse = await window.gapi.client.drive.files.list({
        q: `name='${fileName}' and parents in '${folderId}' and trashed=false`,
        fields: 'files(id)',
      })

      const existingFileId = existingFileResponse.result.files?.[0]?.id

      if (existingFileId) {
        // Update existing file
        const response = await window.gapi.client.request({
          path: `https://www.googleapis.com/upload/drive/v3/files/${existingFileId}`,
          method: 'PATCH',
          params: {
            uploadType: 'media',
          },
          headers: {
            'Content-Type': mimeType,
          },
          body: content,
        })
        return existingFileId
      } else {
        // Create new file
        const response = await window.gapi.client.request<{ id: string }>({
          path: 'https://www.googleapis.com/upload/drive/v3/files',
          method: 'POST',
          params: {
            uploadType: 'multipart',
          },
          headers: {
            'Content-Type': 'multipart/related; boundary="foo_bar_baz"',
          },
          body: this.createMultipartBody(fileName, content, mimeType, folderId),
        })
        return response.result.id
      }
    } catch (error) {
      console.error(`Failed to save file ${fileName}:`, error)
      throw error
    }
  }

  // Helper: Create multipart body for file upload
  private createMultipartBody(
    fileName: string,
    content: string,
    mimeType: string,
    parentId: string
  ): string {
    const metadata = {
      name: fileName,
      parents: [parentId],
    }

    const delimiter = 'foo_bar_baz'
    const close_delim = `\r\n--${delimiter}--`

    let body = `--${delimiter}\r\n`
    body += 'Content-Type: application/json\r\n\r\n'
    body += JSON.stringify(metadata) + '\r\n'
    body += `--${delimiter}\r\n`
    body += `Content-Type: ${mimeType}\r\n\r\n`
    body += content
    body += close_delim

    return body
  }

  // Update existing project
  async updateProject(
    projectId: string,
    name: string,
    data: string,
    tags: string[]
  ): Promise<void> {
    const existingProjects = await this.listProjects()
    const project = existingProjects.find(p => p.id === projectId)

    if (!project) {
      throw new Error('Project not found')
    }

    // Use the standard saveProject method which now uses ProjectExporter
    await this.saveProject(projectId, name, data, tags)
  }

  // Delete project from Google Drive
  async deleteProject(projectId: string): Promise<void> {
    if (!this.isReady()) {
      throw new Error('Google Drive service not ready')
    }

    const existingProjects = await this.listProjects()
    const project = existingProjects.find(p => p.id === projectId)

    if (!project) {
      throw new Error('Project not found')
    }

    try {
      // Set auth token before API call
      this.setAuthToken()

      await window.gapi.client.drive.files.delete({
        fileId: project.driveFileId!,
      })
    } catch (error) {
      console.error('Delete project failed:', error)
      throw new Error('Falha ao excluir projeto do Google Drive')
    }
  }

  // Get project by ID with full data
  async getProject(projectId: string): Promise<GoogleDriveProjectData | null> {
    console.log(`GoogleDriveService.getProject called with ID: ${projectId}`)

    if (!this.isReady()) {
      console.error("GoogleDriveService not ready in getProject")
      return null
    }

    try {
      this.setAuthToken()

      // Find project folder
      const projectFolderName = `projeto-${projectId}`
      console.log(`Looking for project folder: ${projectFolderName}`)
      const projectFolderId = await this.findProjectFolder(projectFolderName)

      if (!projectFolderId) {
        console.error(`Project folder not found: ${projectFolderName}`)
        return null
      }

      console.log(`Found project folder with ID: ${projectFolderId}`)

      // Get metadata from index.json
      const indexFileResponse = await window.gapi.client.drive.files.list({
        q: `'${projectFolderId}' in parents and name='index.json' and trashed=false`,
        fields: 'files(id)',
      })

      const indexFile = indexFileResponse.result.files?.[0]
      if (!indexFile) {
        console.error("index.json file not found in project folder")
        return null
      }

      console.log(`Found index.json with ID: ${indexFile.id}`)
      const indexData = await this.downloadJsonFile(indexFile.id!)
      if (!indexData) {
        console.error("Failed to download or parse index.json")
        return null
      }

      console.log("Downloaded index.json:", indexData)

      // Get data from data.block-content-editor file (updated to use new filename)
      const dataFileResponse = await window.gapi.client.drive.files.list({
        q: `'${projectFolderId}' in parents and name='data.block-content-editor' and trashed=false`,
        fields: 'files(id)',
      })

      const dataFile = dataFileResponse.result.files?.[0]
      if (!dataFile) {
        console.error("data.block-content-editor file not found in project folder")
        return null
      }

      console.log(`Found data.block-content-editor with ID: ${dataFile.id}`)
      const dataResponse = await window.gapi.client.drive.files.get({
        fileId: dataFile.id!,
        alt: 'media',
      })

      console.log("Downloaded data.block-content-editor content length:", dataResponse.body?.length)

      // Use ProjectImporter to process the folder structure
      const folderData: FolderStructureData = {
        indexContent: JSON.stringify(indexData),
        dataContent: dataResponse.body,
        folderName: projectFolderName
      }

      console.log("Calling ProjectImporter.importFromFolderStructure with:", {
        indexContentLength: folderData.indexContent.length,
        dataContentLength: folderData.dataContent.length,
        folderName: folderData.folderName
      })

      const importedProject = await ProjectImporter.importFromFolderStructure(folderData)

      console.log("ProjectImporter returned:", {
        id: importedProject?.id,
        name: importedProject?.name,
        dataLength: importedProject?.data?.length,
        tagsCount: importedProject?.tags?.length
      })

      // Validate that the imported project has the required data
      if (!importedProject || !importedProject.data) {
        console.error('ProjectImporter returned invalid data:', importedProject)
        return null
      }

      // Validate that the data is valid JSON
      try {
        JSON.parse(importedProject.data)
        console.log("Project data is valid JSON")
      } catch (parseError) {
        console.error('Project data is not valid JSON:', parseError, importedProject.data)
        return null
      }

      // Convert to GoogleDriveProjectData format
      const projectData = {
        id: importedProject.id,
        name: importedProject.name,
        data: importedProject.data,
        tags: importedProject.tags,
        size: importedProject.metadata?.size || new Blob([importedProject.data]).size,
        createdAt: importedProject.metadata?.createdAt || new Date().toISOString(),
        updatedAt: importedProject.metadata?.updatedAt || new Date().toISOString(),
        hash: importedProject.metadata?.hash || '',
        driveFileId: projectFolderId,
        storageType: "google-drive" as const
      }

      // Final validation
      if (!isGoogleDriveProjectContent(projectData)) {
        console.error('Generated project data is invalid:', projectData)
        return null
      }

      console.log("Returning valid project data:", {
        id: projectData.id,
        name: projectData.name,
        dataLength: projectData.data.length,
        storageType: projectData.storageType
      })

      return projectData

    } catch (error) {
      console.error(`Failed to get project ${projectId}:`, error)
      return null
    }
  }

  // Search projects by name or tags
  async searchProjects(query: string): Promise<GoogleDriveProjectData[]> {
    const allProjects = await this.listProjects()

    if (!query.trim()) {
      return allProjects
    }

    const searchTerm = query.toLowerCase()

    return allProjects.filter(project =>
      project.name.toLowerCase().includes(searchTerm) ||
      project.tags.some(tag => tag.toLowerCase().includes(searchTerm))
    )
  }

  // Get storage statistics
  async getStorageStats(): Promise<{ totalSize: number; projectCount: number }> {
    const projects = await this.listProjects()

    return {
      totalSize: projects.reduce((total, project) => total + (project.size || 0), 0),
      projectCount: projects.length,
    }
  }

  // Cleanup - reset authentication
  cleanup() {
    this.accessToken = null
    this.folderId = null
  }
}
