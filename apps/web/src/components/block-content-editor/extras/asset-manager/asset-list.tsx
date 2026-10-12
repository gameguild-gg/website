import { MoreVertical, Download, Trash2, FileImage, FileText, FileVideo, File, Edit } from "lucide-react"
import { Button } from "@game-guild/ui/components/button"
import { Badge } from "@game-guild/ui/components/badge"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@game-guild/ui/components/dropdown-menu"
import { useEffect, useState } from "react"
import { toAssetUri } from "@game-guild/assets"
import { getDefaultBrowserAssetRepository } from "@game-guild/assets/browser"

const assetRepository = getDefaultBrowserAssetRepository()

interface Asset {
  id: string
  name: string
  mimeType: string
  size: number
  createdAt: string
  projects?: string[]
}

interface AssetListProps {
  assets: Asset[]
  viewMode: 'list' | 'grid'
  gridColumns?: number
  listColumns?: number
  onDelete: (assetId: string, assetName: string) => void
  onDownload: (assetId: string, assetName: string) => void
  onEdit: (assetId: string, currentName: string) => void
}

export function AssetList({ assets, viewMode, gridColumns = 5, listColumns = 1, onDelete, onDownload, onEdit }: AssetListProps) {
  const [assetDataUrls, setAssetDataUrls] = useState<Record<string, string>>({})

  // Generate grid columns class based on columns prop
  const getGridClass = () => {
    const colMap: Record<number, string> = {
      5: 'grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5',
      6: 'grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6',
      7: 'grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-7',
      9: 'grid-cols-3 sm:grid-cols-4 md:grid-cols-5 lg:grid-cols-7 xl:grid-cols-9',
      12: 'grid-cols-3 sm:grid-cols-6 md:grid-cols-8 lg:grid-cols-10 xl:grid-cols-12',
    }
    return colMap[gridColumns] || colMap[5]
  }

  // Load asset data URLs for thumbnails
  useEffect(() => {
    let active = true
    const releases: Array<() => void> = []
    const loadAssetData = async () => {
      const urls: Record<string, string> = {}
      for (const asset of assets) {
        if (asset.mimeType.startsWith('image/')) {
          try {
            const resolved = await assetRepository.createObjectUrl(toAssetUri(asset.id))
            if (!active) resolved.release()
            else {
              urls[asset.id] = resolved.url
              releases.push(resolved.release)
            }
          } catch (error) {
            console.error(`Failed to load asset ${asset.id}:`, error)
          }
        }
      }
      if (active) setAssetDataUrls(urls)
    }

    if (assets.length > 0) {
      loadAssetData()
    }
    return () => {
      active = false
      releases.forEach((release) => release())
    }
  }, [assets])
  const getFileIcon = (mimeType: string) => {
    if (mimeType.startsWith('image/')) return <FileImage className="w-8 h-8 text-blue-500" />
    if (mimeType.startsWith('video/')) return <FileVideo className="w-8 h-8 text-purple-500" />
    if (mimeType.startsWith('text/')) return <FileText className="w-8 h-8 text-green-500" />
    return <File className="w-8 h-8 text-gray-500" />
  }

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  }

  const formatDate = (dateString: string): string => {
    const date = new Date(dateString)
    return new Intl.DateTimeFormat('pt-BR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    }).format(date)
  }

  const formatMimeType = (mimeType: string): { label: string; color: string; bgColor: string } => {
    const [type, subtype] = mimeType.split('/')
    
    // Map common types to friendly names
    const typeMap: Record<string, { label: string; color: string; bgColor: string }> = {
      'image/jpeg': { label: 'JPEG', color: 'text-blue-700 dark:text-blue-300', bgColor: 'bg-blue-50 dark:bg-blue-950/50 border-blue-200 dark:border-blue-800' },
      'image/jpg': { label: 'JPG', color: 'text-blue-700 dark:text-blue-300', bgColor: 'bg-blue-50 dark:bg-blue-950/50 border-blue-200 dark:border-blue-800' },
      'image/png': { label: 'PNG', color: 'text-green-700 dark:text-green-300', bgColor: 'bg-green-50 dark:bg-green-950/50 border-green-200 dark:border-green-800' },
      'image/gif': { label: 'GIF', color: 'text-purple-700 dark:text-purple-300', bgColor: 'bg-purple-50 dark:bg-purple-950/50 border-purple-200 dark:border-purple-800' },
      'image/webp': { label: 'WebP', color: 'text-cyan-700 dark:text-cyan-300', bgColor: 'bg-cyan-50 dark:bg-cyan-950/50 border-cyan-200 dark:border-cyan-800' },
      'image/svg+xml': { label: 'SVG', color: 'text-orange-700 dark:text-orange-300', bgColor: 'bg-orange-50 dark:bg-orange-950/50 border-orange-200 dark:border-orange-800' },
      'video/mp4': { label: 'MP4', color: 'text-purple-700 dark:text-purple-300', bgColor: 'bg-purple-50 dark:bg-purple-950/50 border-purple-200 dark:border-purple-800' },
      'video/webm': { label: 'WebM', color: 'text-purple-700 dark:text-purple-300', bgColor: 'bg-purple-50 dark:bg-purple-950/50 border-purple-200 dark:border-purple-800' },
      'text/plain': { label: 'TXT', color: 'text-gray-700 dark:text-gray-300', bgColor: 'bg-gray-50 dark:bg-gray-950/50 border-gray-200 dark:border-gray-800' },
      'application/pdf': { label: 'PDF', color: 'text-red-700 dark:text-red-300', bgColor: 'bg-red-50 dark:bg-red-950/50 border-red-200 dark:border-red-800' },
    }

    if (typeMap[mimeType]) {
      return typeMap[mimeType]
    }

    // Fallback to generic type-based coloring
    if (type === 'image') {
      return { label: (subtype || 'IMAGE').toUpperCase(), color: 'text-blue-700 dark:text-blue-300', bgColor: 'bg-blue-50 dark:bg-blue-950/50 border-blue-200 dark:border-blue-800' }
    }
    if (type === 'video') {
      return { label: (subtype || 'VIDEO').toUpperCase(), color: 'text-purple-700 dark:text-purple-300', bgColor: 'bg-purple-50 dark:bg-purple-950/50 border-purple-200 dark:border-purple-800' }
    }
    if (type === 'audio') {
      return { label: (subtype || 'AUDIO').toUpperCase(), color: 'text-pink-700 dark:text-pink-300', bgColor: 'bg-pink-50 dark:bg-pink-950/50 border-pink-200 dark:border-pink-800' }
    }
    if (type === 'text') {
      return { label: (subtype || 'TEXT').toUpperCase(), color: 'text-green-700 dark:text-green-300', bgColor: 'bg-green-50 dark:bg-green-950/50 border-green-200 dark:border-green-800' }
    }

    return { label: (subtype || type || 'FILE').toUpperCase(), color: 'text-gray-700 dark:text-gray-300', bgColor: 'bg-gray-50 dark:bg-gray-950/50 border-gray-200 dark:border-gray-800' }
  }

  if (viewMode === 'grid') {
    return (
      <div className={`grid ${getGridClass()} gap-4`}>
        {assets.map((asset) => (
          <div
            key={asset.id}
            className="group border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden hover:shadow-lg hover:border-gray-300 dark:hover:border-gray-600 transition-all duration-200 bg-white dark:bg-gray-800"
          >
            <div className="aspect-square bg-gray-100 dark:bg-gray-900 flex items-center justify-center overflow-hidden min-h-[120px]">
              {asset.mimeType.startsWith('image/') && assetDataUrls[asset.id] ? (
                <img 
                  src={assetDataUrls[asset.id]} 
                  alt={asset.name}
                  className="w-full h-full object-cover"
                />
              ) : (
                <div className="p-6">
                  {getFileIcon(asset.mimeType)}
                </div>
              )}
            </div>
            <div className="p-2 sm:p-3 min-h-[70px] sm:min-h-[80px]">
              <div className="flex items-start justify-between gap-1 sm:gap-2">
                <div className="flex-1 min-w-0">
                  <p className="text-xs sm:text-sm font-semibold text-gray-900 dark:text-gray-100 truncate mb-1 leading-tight" title={asset.name}>
                    {asset.name}
                  </p>
                  <div className="flex items-center gap-1 sm:gap-2 mt-1 flex-wrap">
                    <span className="text-[10px] sm:text-xs text-gray-500 dark:text-gray-400">{formatFileSize(asset.size)}</span>
                    <Badge 
                      variant="outline" 
                      className={`text-[9px] sm:text-xs font-medium px-1 sm:px-1.5 py-0 h-4 sm:h-5 ${formatMimeType(asset.mimeType).color} ${formatMimeType(asset.mimeType).bgColor} border`}
                    >
                      {formatMimeType(asset.mimeType).label}
                    </Badge>
                  </div>
                  {asset.projects && asset.projects.length > 0 && (
                    <Badge variant="secondary" className="mt-1 text-[9px] sm:text-xs hidden sm:inline-flex">
                      {asset.projects.length} project{asset.projects.length !== 1 ? 's' : ''}
                    </Badge>
                  )}
                </div>
                <DropdownMenu>
                  <DropdownMenuTrigger render={<Button variant="ghost" size="sm" className="h-7 w-7 sm:h-8 sm:w-8 p-0 shrink-0" />}>
                    <MoreVertical className="w-3 h-3 sm:w-4 sm:h-4" />
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onClick={() => onEdit(asset.id, asset.name)}>
                      <Edit className="w-4 h-4 mr-2" />
                      Rename
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={() => onDownload(asset.id, asset.name)}>
                      <Download className="w-4 h-4 mr-2" />
                      Download
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      onClick={() => { onDelete(asset.id, asset.name); }}
                      className="text-red-600 dark:text-red-400"
                    >
                      <Trash2 className="w-4 h-4 mr-2" />
                      Delete
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </div>
            </div>
          </div>
        ))}
      </div>
    )
  }

  return (
    <div className={listColumns === 2 ? "grid grid-cols-1 lg:grid-cols-2 gap-4" : "space-y-2"}>
      {assets.map((asset) => (
        <div
          key={asset.id}
          className="group border border-gray-200 dark:border-gray-700 rounded-lg p-4 hover:shadow-lg hover:border-gray-300 dark:hover:border-gray-600 transition-all duration-200 bg-white dark:bg-gray-800"
        >
          <div className="flex items-center gap-4">
            <div className="shrink-0 w-16 h-16 bg-gray-100 dark:bg-gray-900 rounded-lg overflow-hidden flex items-center justify-center">
              {asset.mimeType.startsWith('image/') && assetDataUrls[asset.id] ? (
                <img 
                  src={assetDataUrls[asset.id]} 
                  alt={asset.name}
                  className="w-full h-full object-cover"
                />
              ) : (
                getFileIcon(asset.mimeType)
              )}
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-gray-900 dark:text-gray-100 truncate" title={asset.name}>
                {asset.name}
              </p>
              <div className="flex items-center gap-3 text-xs text-gray-500 dark:text-gray-400 flex-wrap">
                <span>{formatFileSize(asset.size)}</span>
                <span>•</span>
                <span>{formatDate(asset.createdAt)}</span>
                <span>•</span>
                <Badge 
                  variant="outline" 
                  className={`text-xs font-medium px-2 py-0 h-5 ${formatMimeType(asset.mimeType).color} ${formatMimeType(asset.mimeType).bgColor} border`}
                >
                  {formatMimeType(asset.mimeType).label}
                </Badge>
                {asset.projects && asset.projects.length > 0 && (
                  <>
                    <span>•</span>
                    <Badge variant="secondary" className="text-xs">
                      {asset.projects.length} project{asset.projects.length !== 1 ? 's' : ''}
                    </Badge>
                  </>
                )}
              </div>
            </div>
            <div className="flex items-center gap-2">
              <DropdownMenu>
                <DropdownMenuTrigger render={<Button variant="ghost" size="sm" className="h-8 w-8 p-0" />}>
                  <MoreVertical className="w-4 h-4" />
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem onClick={() => onEdit(asset.id, asset.name)}>
                    <Edit className="w-4 h-4 mr-2" />
                    Rename
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => onDownload(asset.id, asset.name)}>
                    <Download className="w-4 h-4 mr-2" />
                    Download
                  </DropdownMenuItem>
                  <DropdownMenuItem
                    onClick={() => onDelete(asset.id, asset.name)}
                    className="text-red-600 dark:text-red-400"
                  >
                    <Trash2 className="w-4 h-4 mr-2" />
                    Delete
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}
