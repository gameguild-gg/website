"use client"

import { useState, useEffect, useCallback } from "react"
import { toast } from "sonner"
import { GoogleDriveSecurity } from "@/components/block-content-editor/utils/editor/google-drive-security"
import { GoogleDriveService } from "@/components/block-content-editor/services/editor/google-drive-service"
import type { GoogleDriveApi, GoogleDriveFolder, GoogleIdentityServices } from "@/components/block-content-editor/lib/api/editor/google-drive-sdk"

interface GoogleDriveAuthState {
  isAuthenticated: boolean
  isLoading: boolean
  selectedFolder: string | null
  folderName: string | null
  accessToken: string | null
  error: string | null
}

// Extend window type for new Google Identity Services
declare global {
  interface Window {
    google: GoogleIdentityServices
    gapi: GoogleDriveApi
  }
}

export function useGoogleDriveAuth() {
  const [authState, setAuthState] = useState<GoogleDriveAuthState>({
    isAuthenticated: false,
    isLoading: false,
    selectedFolder: null,
    folderName: null,
    accessToken: null,
    error: null,
  })

  // Helper function to update localStorage and notify other instances
  const updateLocalStorage = useCallback((key: string, value: string) => {
    localStorage.setItem(key, value)
    // Dispatch custom event for same-tab updates
    window.dispatchEvent(new CustomEvent('block-content-editor-storage-change', { 
      detail: { key, value } 
    }))
  }, [])

  // Google Drive API configuration
  const CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_DRIVE_CLIENT_ID
  const API_KEY = process.env.NEXT_PUBLIC_GOOGLE_DRIVE_API_KEY
  const DISCOVERY_DOC = 'https://www.googleapis.com/discovery/v1/apis/drive/v3/rest'
  
  // Minimal required scopes - only file access within app-created folders
  const SCOPES = 'https://www.googleapis.com/auth/drive.file'

  // Configure GoogleDriveService when authentication state changes
  useEffect(() => {
    const googleDriveService = GoogleDriveService.getInstance()
    
    if (authState.isAuthenticated && authState.accessToken && authState.selectedFolder) {
      // Configure the service with authentication credentials
      googleDriveService.setAuth(authState.accessToken, authState.selectedFolder)
      console.log('GoogleDriveService configured with auth credentials')
    }
  }, [authState.isAuthenticated, authState.accessToken, authState.selectedFolder])

  // Load Google APIs (both GIS and GAPI)
  const loadGoogleAPI = useCallback(async () => {
    if (typeof window === 'undefined') return false

    try {
      // Load Google Identity Services (new auth)
      if (!window.google) {
        await new Promise<void>((resolve, reject) => {
          const script = document.createElement('script')
          script.src = 'https://accounts.google.com/gsi/client'
          script.onload = () => resolve()
          script.onerror = () => reject(new Error('Failed to load Google Identity Services'))
          document.head.appendChild(script)
        })
      }

      // Load Google API script (for Drive API)
      if (!window.gapi) {
        await new Promise<void>((resolve, reject) => {
          const script = document.createElement('script')
          script.src = 'https://apis.google.com/js/api.js'
          script.onload = () => resolve()
          script.onerror = () => reject(new Error('Failed to load Google API'))
          document.head.appendChild(script)
        })
      }

      // Initialize GAPI for Drive API
      await new Promise<void>((resolve) => {
        window.gapi.load('client', resolve)
      })

      // Initialize client
      await window.gapi.client.init({
        apiKey: API_KEY,
        discoveryDocs: [DISCOVERY_DOC],
      })

      return true
    } catch (error) {
      console.error('Failed to load Google API:', error)
      setAuthState(prev => ({ 
        ...prev, 
        error: 'Falha ao carregar API do Google Drive',
        isLoading: false 
      }))
      return false
    }
  }, [API_KEY])

  // Check authentication status
  const checkAuthStatus = useCallback(async () => {
    // Check if we have a stored access token
    const storedToken = localStorage.getItem('block-content-editor_google_drive_token')
    const storedExpiry = localStorage.getItem('block-content-editor_google_drive_token_expiry')
    
    if (storedToken && storedExpiry) {
      const expiryTime = parseInt(storedExpiry)
      const now = Date.now()
      
      if (now < expiryTime) {
        // Token is still valid
        const savedFolder = localStorage.getItem('block-content-editor_google_drive_folder')
        const savedFolderName = localStorage.getItem('block-content-editor_google_drive_folder_name')
        
        setAuthState(prev => ({
          ...prev,
          isAuthenticated: true,
          accessToken: storedToken,
          selectedFolder: savedFolder,
          folderName: savedFolderName,
          error: null,
        }))
        return
      } else {
        // Token expired, clean up
        localStorage.removeItem('block-content-editor_google_drive_token')
        localStorage.removeItem('block-content-editor_google_drive_token_expiry')
      }
    }
  }, [])

  // Initialize Google Drive API
  useEffect(() => {
    const initialize = async () => {
      // Validate environment first
      const envValidation = GoogleDriveSecurity.validateEnvironment()
      if (!envValidation.isValid) {
        console.warn('Google Drive API not properly configured:', envValidation.errors)
        return
      }

      // Clean up expired data
      GoogleDriveSecurity.cleanupExpiredData()

      setAuthState(prev => ({ ...prev, isLoading: true }))
      
      const loaded = await loadGoogleAPI()
      if (loaded) {
        await checkAuthStatus()
      }
      
      setAuthState(prev => ({ ...prev, isLoading: false }))
    }

    initialize()
  }, [loadGoogleAPI, checkAuthStatus, CLIENT_ID, API_KEY])

  // Authenticate with Google using new GIS
  const authenticate = useCallback(async (): Promise<boolean> => {
    if (!window.google || !CLIENT_ID) {
      toast.error('Google API não carregada')
      return false
    }

    try {
      setAuthState(prev => ({ ...prev, isLoading: true, error: null }))
      
      // Use new Google Identity Services
      return await new Promise<boolean>((resolve) => {
        const handleAuthenticationError = (errorCode: string) => {
          let errorMessage = 'Falha na autenticação'
          if (errorCode === 'popup_closed_by_user' || errorCode === 'popup_closed') {
            errorMessage = 'Autenticação cancelada pelo usuário'
          } else if (errorCode === 'access_denied') {
            errorMessage = 'Acesso negado pelo usuário'
          }

          setAuthState(prev => ({
            ...prev,
            error: errorMessage,
            isLoading: false
          }))

          toast.error(errorMessage)
          resolve(false)
        }

        const tokenClient = window.google.accounts.oauth2.initTokenClient({
          client_id: CLIENT_ID,
          scope: SCOPES,
          callback: async (response) => {
            if (response.error !== undefined) {
              console.error('Google authentication failed:', response)
              handleAuthenticationError(response.error)
              return
            }

            // Success - we have an access token
            const accessToken = response.access_token
            const expiresIn = response.expires_in ? parseInt(String(response.expires_in)) * 1000 : 3600000 // Default 1 hour
            const expiryTime = Date.now() + expiresIn

            // Store token with expiry
            updateLocalStorage('block-content-editor_google_drive_token', accessToken)
            updateLocalStorage('block-content-editor_google_drive_token_expiry', expiryTime.toString())
            updateLocalStorage('block-content-editor_google_drive_auth_time', Date.now().toString())

            setAuthState(prev => ({
              ...prev,
              isAuthenticated: true,
              accessToken: accessToken,
              isLoading: false,
              error: null,
            }))

            GoogleDriveSecurity.logSecurityEvent('google_drive_auth_success')
            toast.success('Autenticado com Google Drive')
            resolve(true)
          },
          error_callback: (error) => {
            console.error('Google authentication failed:', error)
            handleAuthenticationError(error.type)
          },
        })

        // Request access token
        tokenClient.requestAccessToken({ prompt: 'consent' })
      })
    } catch (error) {
      console.error('Google authentication failed:', error)
      
      setAuthState(prev => ({ 
        ...prev, 
        error: 'Falha na autenticação',
        isLoading: false 
      }))
      
      toast.error('Falha na autenticação')
      return false
    }
  }, [CLIENT_ID, SCOPES])

  // Create or find Block Content Editor folder
  const createOrFindFolder = useCallback(async (folderName: string): Promise<string | null> => {
    const accessToken = authState.accessToken
    if (!accessToken) return null

    // Sanitize folder name
    const sanitizedName = GoogleDriveSecurity.sanitizeFileName(folderName.replace('.block-content-editor.json', ''))
    const finalFolderName = sanitizedName.replace('.block-content-editor.json', '') // Remove extension for folder name

    try {
      return await GoogleDriveSecurity.throttleApiCall(async () => {
        // Set the access token for GAPI client before making API calls
        window.gapi.client.setToken({ access_token: accessToken })
        
        // First, check if folder already exists
        const searchResponse = await window.gapi.client.drive.files.list({
          q: `name='${finalFolderName}' and mimeType='application/vnd.google-apps.folder' and trashed=false`,
          fields: 'files(id, name, createdTime)',
        })

        if (searchResponse.result.files && searchResponse.result.files.length > 0) {
          const folder = searchResponse.result.files[0]
          
          // Save folder info
          updateLocalStorage('block-content-editor_google_drive_folder', folder.id!)
          updateLocalStorage('block-content-editor_google_drive_folder_name', folder.name!)
          
          setAuthState(prev => ({
            ...prev,
            selectedFolder: folder.id!,
            folderName: folder.name!,
          }))
          
          return folder.id!
        }

        // Create new folder if not found
        const createResponse = await window.gapi.client.drive.files.create({
          resource: {
            name: finalFolderName,
            mimeType: 'application/vnd.google-apps.folder',
          },
          fields: 'id, name',
        })

        const folderId = createResponse.result.id!
        
        // Save folder info
        updateLocalStorage('block-content-editor_google_drive_folder', folderId)
        updateLocalStorage('block-content-editor_google_drive_folder_name', finalFolderName)
        
        setAuthState(prev => ({
          ...prev,
          selectedFolder: folderId,
          folderName: finalFolderName,
        }))

        toast.success(`Pasta "${finalFolderName}" criada no Google Drive`)
        return folderId
      })
    } catch (error) {
      console.error('Failed to create/find folder:', error)
      GoogleDriveSecurity.logSecurityEvent('folder_creation_failed', { error: String(error) })
      toast.error('Falha ao criar pasta no Google Drive')
      return null
    }
  }, [authState.accessToken])

  // Sign out
  const signOut = useCallback(async () => {
    try {
      // Revoke the access token
      const accessToken = authState.accessToken
      if (accessToken) {
        // Revoke token using new GIS method
        window.google?.accounts?.oauth2?.revoke(accessToken, () => {
          console.log('Token revoked')
        })
      }
      
      // Clear stored data
      localStorage.removeItem('block-content-editor_google_drive_token')
      localStorage.removeItem('block-content-editor_google_drive_token_expiry')
      localStorage.removeItem('block-content-editor_google_drive_folder')
      localStorage.removeItem('block-content-editor_google_drive_folder_name')
      localStorage.removeItem('block-content-editor_google_drive_auth_time')
      
      setAuthState({
        isAuthenticated: false,
        isLoading: false,
        selectedFolder: null,
        folderName: null,
        accessToken: null,
        error: null,
      })

      toast.success('Desconectado do Google Drive')
    } catch (error) {
      console.error('Sign out failed:', error)
      toast.error('Falha ao desconectar')
    }
  }, [authState.accessToken])

  // Get available folders (for selection)
  const getAvailableFolders = useCallback(async (): Promise<GoogleDriveFolder[]> => {
    if (!authState.accessToken) return []

    try {
      // Set the access token for GAPI client
      window.gapi.client.setToken({ access_token: authState.accessToken })
      
      const response = await window.gapi.client.drive.files.list({
        q: "mimeType='application/vnd.google-apps.folder' and trashed=false",
        fields: 'files(id, name, createdTime)',
        orderBy: 'name',
      })

      return response.result.files || []
    } catch (error) {
      console.error('Failed to get folders:', error)
      return []
    }
  }, [authState.accessToken])

  // Add a refresh function to force state update
  const refreshAuthState = useCallback(() => {
    const storedToken = localStorage.getItem('block-content-editor_google_drive_token')
    const storedExpiry = localStorage.getItem('block-content-editor_google_drive_token_expiry')
    
    if (storedToken && storedExpiry) {
      const expiryTime = parseInt(storedExpiry)
      const now = Date.now()
      
      if (now < expiryTime) {
        // Token is still valid
        const savedFolder = localStorage.getItem('block-content-editor_google_drive_folder')
        const savedFolderName = localStorage.getItem('block-content-editor_google_drive_folder_name')
        
        setAuthState(prev => ({
          ...prev,
          isAuthenticated: true,
          accessToken: storedToken,
          selectedFolder: savedFolder,
          folderName: savedFolderName,
          error: null,
        }))
      } else {
        // Token expired, clean up
        localStorage.removeItem('block-content-editor_google_drive_token')
        localStorage.removeItem('block-content-editor_google_drive_token_expiry')
        setAuthState(prev => ({
          ...prev,
          isAuthenticated: false,
          accessToken: null,
          selectedFolder: null,
          folderName: null,
        }))
      }
    } else {
      setAuthState(prev => ({
        ...prev,
        isAuthenticated: false,
        accessToken: null,
        selectedFolder: null,
        folderName: null,
      }))
    }
  }, [])

  // Listen for localStorage changes to automatically refresh auth state
  useEffect(() => {
    const handleStorageChange = (e: StorageEvent) => {
      if (e.key === 'block-content-editor_google_drive_token' || 
          e.key === 'block-content-editor_google_drive_token_expiry' ||
          e.key === 'block-content-editor_google_drive_folder') {
        // Token or folder changed, refresh auth state
        refreshAuthState()
      }
    }

    const handleCustomStorageChange = (e: CustomEvent) => {
      const { key } = e.detail
      if (key === 'block-content-editor_google_drive_token' || 
          key === 'block-content-editor_google_drive_token_expiry' ||
          key === 'block-content-editor_google_drive_folder') {
        // Token or folder changed, refresh auth state
        refreshAuthState()
      }
    }

    window.addEventListener('storage', handleStorageChange)
    window.addEventListener('block-content-editor-storage-change', handleCustomStorageChange as EventListener)
    
    return () => {
      window.removeEventListener('storage', handleStorageChange)
      window.removeEventListener('block-content-editor-storage-change', handleCustomStorageChange as EventListener)
    }
  }, [refreshAuthState])

  return {
    ...authState,
    authenticate,
    signOut,
    createOrFindFolder,
    getAvailableFolders,
    refreshAuthState,
    hasValidSetup: authState.isAuthenticated && authState.selectedFolder,
  }
}
