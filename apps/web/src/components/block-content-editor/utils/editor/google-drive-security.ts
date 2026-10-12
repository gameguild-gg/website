"use client"

/**
 * Security utilities for Google Drive integration
 * Implements additional security measures and validation
 */

interface TokenValidationResult {
  isValid: boolean
  expiresIn?: number
  error?: string
}

export class GoogleDriveSecurity {
  private static readonly MAX_TOKEN_AGE = 3600000 // 1 hour in milliseconds
  private static readonly ALLOWED_FILE_TYPES = ['application/json']
  private static readonly MAX_FILE_SIZE = 10 * 1024 * 1024 // 10MB

  /**
   * Validate access token and check expiration
   */
  static async validateToken(accessToken: string): Promise<TokenValidationResult> {
    try {
      const response = await fetch(
        `https://www.googleapis.com/oauth2/v1/tokeninfo?access_token=${accessToken}`
      )

      if (!response.ok) {
        return { isValid: false, error: 'Token validation failed' }
      }

      const tokenInfo = await response.json()

      // Check if token has expired
      const expiresIn = parseInt(tokenInfo.expires_in || '0')
      if (expiresIn <= 0) {
        return { isValid: false, error: 'Token has expired' }
      }

      // Check if token has the required scope
      const scopes = tokenInfo.scope?.split(' ') || []
      if (!scopes.includes('https://www.googleapis.com/auth/drive.file')) {
        return { isValid: false, error: 'Insufficient permissions' }
      }

      return { isValid: true, expiresIn }
    } catch (error) {
      console.error('Token validation error:', error)
      return { isValid: false, error: 'Token validation failed' }
    }
  }

  /**
   * Sanitize and validate file names
   */
  static sanitizeFileName(fileName: string): string {
    // Remove potentially dangerous characters
    const sanitized = fileName
      .replace(/[<>:"/\\|?*]/g, '_') // Replace invalid characters with underscore
      .replace(/\.\./g, '_') // Prevent directory traversal
      .replace(/^\./, '_') // Prevent hidden files
      .trim()

    // Ensure file has .block-content-editor.json extension
    if (!sanitized.endsWith('.block-content-editor.json')) {
      const baseName = sanitized.replace(/\.json$/, '').replace(/\.block-content-editor$/, '')
      return `${baseName}.block-content-editor.json`
    }

    return sanitized
  }

  /**
   * Validate project data before uploading
   */
  static validateProjectData(data: any): { isValid: boolean; error?: string } {
    if (!data || typeof data !== 'object') {
      return { isValid: false, error: 'Invalid data format' }
    }

    const requiredFields = ['id', 'name', 'data', 'tags', 'createdAt', 'updatedAt']
    for (const field of requiredFields) {
      if (!(field in data)) {
        return { isValid: false, error: `Missing required field: ${field}` }
      }
    }

    // Validate field types
    if (typeof data.id !== 'string' || data.id.length === 0) {
      return { isValid: false, error: 'Invalid project ID' }
    }

    if (typeof data.name !== 'string' || data.name.length === 0) {
      return { isValid: false, error: 'Invalid project name' }
    }

    if (typeof data.data !== 'string') {
      return { isValid: false, error: 'Invalid project data' }
    }

    if (!Array.isArray(data.tags)) {
      return { isValid: false, error: 'Invalid tags format' }
    }

    // Validate dates
    const createdAt = new Date(data.createdAt)
    const updatedAt = new Date(data.updatedAt)
    
    if (isNaN(createdAt.getTime()) || isNaN(updatedAt.getTime())) {
      return { isValid: false, error: 'Invalid date format' }
    }

    // Check data size
    const dataSize = JSON.stringify(data).length
    if (dataSize > this.MAX_FILE_SIZE) {
      return { isValid: false, error: 'Project data too large' }
    }

    return { isValid: true }
  }

  /**
   * Generate secure project ID
   */
  static generateSecureId(): string {
    const timestamp = Date.now().toString(36)
    const randomPart = Math.random().toString(36).substr(2, 9)
    return `ggl_${timestamp}_${randomPart}`
  }

  /**
   * Encrypt sensitive data before storing (simple obfuscation)
   */
  static obfuscateData(data: string): string {
    // Simple base64 encoding for basic obfuscation
    // In production, consider using proper encryption
    try {
      return btoa(unescape(encodeURIComponent(data)))
    } catch (error) {
      console.error('Data obfuscation failed:', error)
      return data
    }
  }

  /**
   * Decrypt obfuscated data
   */
  static deobfuscateData(data: string): string {
    try {
      return decodeURIComponent(escape(atob(data)))
    } catch (error) {
      console.error('Data deobfuscation failed:', error)
      return data
    }
  }

  /**
   * Rate limiting for API calls
   */
  private static lastApiCall = 0
  private static readonly MIN_API_INTERVAL = 100 // 100ms between API calls

  static async throttleApiCall<T>(apiCall: () => Promise<T>): Promise<T> {
    const now = Date.now()
    const timeSinceLastCall = now - this.lastApiCall

    if (timeSinceLastCall < this.MIN_API_INTERVAL) {
      await new Promise(resolve => 
        setTimeout(resolve, this.MIN_API_INTERVAL - timeSinceLastCall)
      )
    }

    this.lastApiCall = Date.now()
    return apiCall()
  }

  /**
   * Validate environment configuration
   */
  static validateEnvironment(): { isValid: boolean; errors: string[] } {
    const errors: string[] = []

    if (!process.env.NEXT_PUBLIC_GOOGLE_DRIVE_CLIENT_ID) {
      errors.push('NEXT_PUBLIC_GOOGLE_DRIVE_CLIENT_ID not configured')
    }

    if (!process.env.NEXT_PUBLIC_GOOGLE_DRIVE_API_KEY) {
      errors.push('NEXT_PUBLIC_GOOGLE_DRIVE_API_KEY not configured')
    }

    // Validate Client ID format (should end with .googleusercontent.com)
    const clientId = process.env.NEXT_PUBLIC_GOOGLE_DRIVE_CLIENT_ID
    if (clientId && !clientId.endsWith('.googleusercontent.com')) {
      errors.push('Invalid Client ID format')
    }

    return {
      isValid: errors.length === 0,
      errors
    }
  }

  /**
   * Clean up expired tokens and data
   */
  static cleanupExpiredData(): void {
    if (typeof window === 'undefined') return

    try {
      // Remove expired authentication data
      const stored = localStorage.getItem('block-content-editor_google_drive_auth_time')
      if (stored) {
        const authTime = parseInt(stored)
        const now = Date.now()
        
        if (now - authTime > this.MAX_TOKEN_AGE) {
          localStorage.removeItem('block-content-editor_google_drive_folder')
          localStorage.removeItem('block-content-editor_google_drive_folder_name')
          localStorage.removeItem('block-content-editor_google_drive_auth_time')
        }
      }
    } catch (error) {
      console.error('Cleanup failed:', error)
    }
  }

  /**
   * Log security events (for monitoring)
   */
  static logSecurityEvent(event: string, details?: unknown): void {
    const securityLog = {
      timestamp: new Date().toISOString(),
      event,
      details,
      userAgent: navigator.userAgent,
      url: window.location.href,
    }

    // In production, send to security monitoring service
    console.log('Security Event:', securityLog)
  }
}
