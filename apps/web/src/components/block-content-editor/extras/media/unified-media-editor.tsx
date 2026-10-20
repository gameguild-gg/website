"use client"

import { useState, useEffect } from "react"
import { Button } from "@game-guild/ui/components/button"
import { Input } from "@game-guild/ui/components/input"
import { Label } from "@game-guild/ui/components/label"
import { Save, Image, Video, Music, FileText, Eye, Grid } from "lucide-react"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@game-guild/ui/components/tabs"
import { useEditorSettings } from "@/components/block-content-editor/extras/settings-menu"
import { BlockEditorShell } from "@/components/block-content-editor/extras/block-editor-shell"
import { MediaPreview } from "./media-preview"
import { ImageOptions } from "./image-options"
import { VideoOptions } from "./video-options"
import { AudioOptions } from "./audio-options"
import { MediaListTab } from "./media-list-tab"
import { LayoutTab } from "./layout-tab"
import { CaptionsTab } from "./captions-tab"
import {
  detectVideoEmbedType,
  detectAudioEmbedType,
  detectVideoFileType,
  detectAudioFileType
} from "./url-detection"
import type { BaseMediaData } from "@/components/block-content-editor/nodes/base/media-node-base"
import { AssetImage } from "./asset-image"

interface UnifiedMediaEditorProps {
  data: BaseMediaData
  onChange: (data: Partial<BaseMediaData>) => void
  onClose?: () => void
  onSave?: (items?: BaseMediaData[], columns?: number, caption?: string) => void
  mode?: "single" | "gallery"
  galleryItems?: BaseMediaData[]
  onGalleryItemsChange?: (items: BaseMediaData[]) => void
  galleryColumns?: number
  onGalleryColumnsChange?: (columns: number) => void
  galleryCaption?: string
  onGalleryCaptionChange?: (caption: string) => void
}

export function UnifiedMediaEditor({ 
  data, 
  onChange, 
  onClose, 
  onSave,
  mode = "single",
  galleryItems = [],
  onGalleryItemsChange,
  galleryColumns = 2,
  onGalleryColumnsChange,
  galleryCaption = "",
  onGalleryCaptionChange
}: UnifiedMediaEditorProps) {
  const [localData, setLocalData] = useState<BaseMediaData>(data)
  const [urlDetectionEnabled, setUrlDetectionEnabled] = useState(true)
  const [activeTab, setActiveTab] = useState<string>("media")
  const settings = useEditorSettings("media")
  
  // Only allow gallery mode for images (video/audio always single)
  const canUseGallery = data.type === "image"
  
  // Sempre trabalha em modo galeria, começando vazio ou com items existentes
  const [localGalleryItems, setLocalGalleryItems] = useState<BaseMediaData[]>(() => {
    if (!canUseGallery) {
      // Para video/audio, sempre usa o item único se tiver src
      return data.src ? [data] : []
    }
    // Para imagens, usa galleryItems se tiver, senão usa data apenas se tiver src válida
    if (galleryItems.length > 0) {
      return galleryItems
    }
    // Só inclui o data inicial se tiver uma src válida (não vazia)
    return data.src && data.src.trim() !== "" ? [data] : []
  })
  const [localColumns, setLocalColumns] = useState(galleryColumns)
  const [localGlobalCaption, setLocalGlobalCaption] = useState(galleryCaption)
  
  // Determina se é galeria baseado no número de itens (apenas para imagens)
  const isGalleryMode = canUseGallery && localGalleryItems.length > 1

  // Sincroniza localData com o primeiro item da galeria ou mantém vazio
  useEffect(() => {
    const firstItem = localGalleryItems[0]
    if (firstItem && !isGalleryMode) {
      setLocalData(firstItem)
    } else if (!firstItem && localGalleryItems.length === 0) {
      // Se não há items, mantém localData com src vazio
      setLocalData(prev => ({ ...prev, src: "" }))
    }
  }, [localGalleryItems, isGalleryMode])

  // Auto-detect embed type when URL changes
  useEffect(() => {
    if (!urlDetectionEnabled || !localData.src) return
    
    if (localData.type === "video") {
      const detectedType = detectVideoEmbedType(localData.src)
      
      if (detectedType !== localData.embedType) {
        const updates: Partial<BaseMediaData> = { embedType: detectedType }
        
        // If it's a direct file, also detect the video format
        if (detectedType === "direct") {
          updates.videoType = detectVideoFileType(localData.src)
        }
        
        const newData = { ...localData, ...updates }
        setLocalData(newData)
        // Don't call onChange here, it will be called on save
      }
    } else if (localData.type === "audio") {
      const detectedType = detectAudioEmbedType(localData.src)
      
      if (detectedType !== localData.embedAudioType) {
        const updates: Partial<BaseMediaData> = { embedAudioType: detectedType }
        
        // If it's a direct file, also detect the audio format
        if (detectedType === "direct") {
          updates.audioType = detectAudioFileType(localData.src)
        }
        
        const newData = { ...localData, ...updates }
        setLocalData(newData)
        // Don't call onChange here, it will be called on save
      }
    }
  }, [localData.src, localData.type, urlDetectionEnabled, localData])

  const handleChange = <Field extends keyof BaseMediaData>(field: Field, value: BaseMediaData[Field]) => {
    const newData = { ...localData, [field]: value }
    setLocalData(newData)
    onChange({ [field]: value })
    
    // Disable auto-detection temporarily when user manually changes embed type
    if (field === "embedType" || field === "embedAudioType") {
      setUrlDetectionEnabled(false)
      // Re-enable after a delay
      setTimeout(() => setUrlDetectionEnabled(true), 2000)
    }
  }
  
  const handleUrlChange = (newUrl: string) => {
    setUrlDetectionEnabled(true) // Re-enable detection on URL change
    handleChange("src", newUrl)
  }

  const handleSave = () => {
    // Filter out items without src that are not placeholders
    const validItems = localGalleryItems.filter(item => 
      item.isPlaceholder || (item.src && item.src.trim() !== "")
    )
    
    if (onSave) {
      // Pass all data to onSave - it will decide what to do
      // Only pass gallery data if canUseGallery is true
      if (canUseGallery) {
        onSave(validItems, localColumns, localGlobalCaption)
      } else {
        // For video/audio, just save the single item if valid
        const itemToSave = validItems.length > 0 ? validItems : [localData]
        onSave(itemToSave, undefined, undefined)
      }
    } else {
      // Fallback to old behavior
      if (isGalleryMode && canUseGallery) {
        // Save gallery data (múltiplos itens) - only for images
        if (onGalleryItemsChange) onGalleryItemsChange(validItems)
        if (onGalleryColumnsChange) onGalleryColumnsChange(localColumns)
        if (onGalleryCaptionChange) onGalleryCaptionChange(localGlobalCaption)
      } else {
        // Save single media data (1 item apenas ou video/audio)
        const singleItem = validItems[0] || localData
        onChange(singleItem)
      }
      
      if (onClose) {
        onClose()
      }
    }
  }

  const handleClose = () => {
    if (onClose) {
      onClose()
    }
  }

  const getMediaIcon = () => {
    switch (localData.type) {
      case "image":
        return <Image className="h-5 w-5 text-blue-600 dark:text-blue-400" />
      case "video":
        return <Video className="h-5 w-5 text-blue-600 dark:text-blue-400" />
      case "audio":
        return <Music className="h-5 w-5 text-blue-600 dark:text-blue-400" />
    }
  }

  const getMediaTitle = () => {
    if (isGalleryMode) {
      return "Gallery Editor"
    }
    const firstItem = localGalleryItems[0] || localData
    switch (firstItem.type) {
      case "image":
        return "Image Editor"
      case "video":
        return "Video Editor"
      case "audio":
        return "Audio Editor"
    }
  }
  
  const getHeaderIcon = () => {
    if (isGalleryMode) {
      return <Grid className="h-5 w-5 text-blue-600 dark:text-blue-400" />
    }
    return getMediaIcon()
  }

  const renderMediaSpecificOptions = () => {
    switch (localData.type) {
      case "video":
        return <VideoOptions data={localData} onChange={handleChange} />
      case "audio":
        return <AudioOptions data={localData} onChange={handleChange} />
      case "image":
      default:
        return <ImageOptions data={localData} onChange={handleChange} />
    }
  }

  return (
    <BlockEditorShell
      settings={settings}
      includeMonacoTheme={false}
      onClose={handleClose}
      icon={getHeaderIcon()}
      title={getMediaTitle()}
      headerMeta={
        isGalleryMode ? (
          <>
            <span className="text-sm text-gray-600 dark:text-gray-400 bg-gray-100 dark:bg-gray-800 px-2 py-1 rounded">
              Items: <span className="font-medium text-gray-800 dark:text-gray-200">{localGalleryItems.length}</span>
            </span>
            <span className="text-sm text-gray-600 dark:text-gray-400 bg-blue-50 dark:bg-blue-900/30 px-2 py-1 rounded">
              Columns: <span className="font-medium text-blue-600 dark:text-blue-400">{localColumns}</span>
            </span>
          </>
        ) : (
          <>
            <span className="text-sm text-gray-600 dark:text-gray-400 bg-gray-100 dark:bg-gray-800 px-2 py-1 rounded">
              Type: <span className="font-medium text-gray-800 dark:text-gray-200 capitalize">{localData.type}</span>
            </span>
            {localData.embedType && localData.embedType !== "direct" && (
              <span className="text-sm text-gray-600 dark:text-gray-400 bg-blue-50 dark:bg-blue-900/30 px-2 py-1 rounded flex items-center gap-1">
                <span className="font-medium text-blue-600 dark:text-blue-400 capitalize">{localData.embedType}</span>
                {urlDetectionEnabled && <span className="text-xs">🔍</span>}
              </span>
            )}
            {localData.embedAudioType && localData.embedAudioType !== "direct" && (
              <span className="text-sm text-gray-600 dark:text-gray-400 bg-blue-50 dark:bg-blue-900/30 px-2 py-1 rounded flex items-center gap-1">
                <span className="font-medium text-blue-600 dark:text-blue-400 capitalize">{localData.embedAudioType}</span>
                {urlDetectionEnabled && <span className="text-xs">🔍</span>}
              </span>
            )}
            {localData.embedType === "direct" && localData.type === "video" && localData.videoType && (
              <span className="text-sm text-gray-600 dark:text-gray-400 bg-green-50 dark:bg-green-900/30 px-2 py-1 rounded">
                <span className="font-medium text-green-600 dark:text-green-400">{localData.videoType}</span>
              </span>
            )}
            {localData.embedAudioType === "direct" && localData.type === "audio" && localData.audioType && (
              <span className="text-sm text-gray-600 dark:text-gray-400 bg-green-50 dark:bg-green-900/30 px-2 py-1 rounded">
                <span className="font-medium text-green-600 dark:text-green-400">{localData.audioType}</span>
              </span>
            )}
          </>
        )
      }
      footer={
        <div className="flex items-center justify-end gap-2">
          <Button
            variant="outline"
            onClick={handleClose}
            className="border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800 bg-transparent"
          >
            Cancel
          </Button>
          <Button
            onClick={handleSave}
            className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 dark:bg-blue-500 dark:hover:bg-blue-600"
          >
            <Save className="h-4 w-4" />
            {isGalleryMode ? "Save Gallery" : "Save Media"}
          </Button>
        </div>
      }
    >
      {/* Main Content */}
      <div className="flex-1 flex min-h-0">
          {/* Left Panel - Configuration */}
          <div className="w-1/2 border-r border-gray-200 dark:border-gray-800 flex flex-col bg-white dark:bg-gray-900">
            <div className="p-4 border-b border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900">
              <h3 className="font-medium flex items-center gap-2 text-gray-800 dark:text-gray-200">
                <FileText className="h-4 w-4 text-blue-600 dark:text-blue-400" />
                Configuration
              </h3>
            </div>

            <div className="flex-1 overflow-y-auto p-4">
              {canUseGallery ? (
                <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
                  <TabsList className="grid w-full grid-cols-3 mb-4">
                    <TabsTrigger value="media">Media</TabsTrigger>
                    <TabsTrigger value="layout">Layout</TabsTrigger>
                    <TabsTrigger value="captions">Captions</TabsTrigger>
                  </TabsList>
                  
                  <TabsContent value="media" className="space-y-4">
                    <MediaListTab
                      items={localGalleryItems}
                      onItemsChange={setLocalGalleryItems}
                      allowMixedTypes={true}
                      defaultType={localData.type}
                    />
                  </TabsContent>
                  
                  <TabsContent value="layout" className="space-y-4">
                    <LayoutTab
                      items={localGalleryItems}
                      onItemsChange={setLocalGalleryItems}
                      columns={localColumns}
                      onColumnsChange={setLocalColumns}
                    />
                  </TabsContent>
                  
                  <TabsContent value="captions" className="space-y-4">
                    <CaptionsTab
                      items={localGalleryItems}
                      onItemsChange={setLocalGalleryItems}
                      globalCaption={localGlobalCaption}
                      onGlobalCaptionChange={setLocalGlobalCaption}
                    />
                  </TabsContent>
                </Tabs>
              ) : (
                <div className="space-y-4">
                  {/* URL Field */}
                  <div className="space-y-2">
                    <Label htmlFor="src" className="text-sm font-medium text-gray-700 dark:text-gray-300">
                      Media URL:
                    </Label>
                    <Input
                      id="src"
                      value={localData.src}
                      onChange={(e) => handleUrlChange(e.target.value)}
                      placeholder={
                        localData.type === "video" 
                          ? "https://youtube.com/... or https://example.com/video.mp4"
                          : localData.type === "audio"
                          ? "https://spotify.com/... or https://example.com/audio.mp3"
                          : "https://example.com/image.jpg"
                      }
                      className="bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 focus:border-blue-500 dark:focus:border-blue-400"
                    />
                    {(localData.type === "video" || localData.type === "audio") && (
                      <p className="text-xs text-gray-500 dark:text-gray-400">
                        💡 Auto-detects YouTube, Vimeo, Spotify, SoundCloud and file URLs
                      </p>
                    )}
                  </div>

                  {/* Media Specific Options - Only show if URL is provided */}
                  {localData.src && (
                    <div className="space-y-3 pt-2">
                      {renderMediaSpecificOptions()}
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>

          {/* Right Panel - Preview */}
          <div className="w-1/2 flex flex-col bg-white dark:bg-gray-900">
            <div className="p-4 border-b border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900">
              <h3 className="font-medium flex items-center gap-2 text-gray-800 dark:text-gray-200">
                <Eye className="h-4 w-4 text-blue-600 dark:text-blue-400" />
                Live Preview
              </h3>
            </div>
            <div className="flex-1 overflow-auto bg-gray-50 dark:bg-gray-950 p-4">
              {isGalleryMode ? (
                localGalleryItems.length > 0 ? (
                  <div className="space-y-4">
                    <div 
                      className="grid gap-3"
                      style={{ gridTemplateColumns: `repeat(${localColumns}, 1fr)` }}
                    >
                      {localGalleryItems.map((item, index) => (
                      <div key={index} className="space-y-2">
                        <div 
                          className="relative overflow-hidden rounded-md bg-gray-200 dark:bg-gray-700"
                          style={{ 
                            width: `${item.size || 100}%`,
                            aspectRatio: item.type === "image" ? "auto" : "16/9"
                          }}
                        >
                          {item.type === "image" && item.src && (
                            <AssetImage src={item.src} alt={item.alt || ""} className="w-full h-auto" />
                          )}
                          {item.type === "video" && (
                            <div className="w-full h-full flex items-center justify-center">
                              <Video className="h-8 w-8 text-gray-400" />
                            </div>
                          )}
                          {item.type === "audio" && (
                            <div className="w-full h-full flex items-center justify-center">
                              <Music className="h-8 w-8 text-gray-400" />
                            </div>
                          )}
                        </div>
                        {item.caption && (
                          <p className="text-xs text-gray-600 dark:text-gray-400 text-center">
                            {item.caption}
                          </p>
                        )}
                      </div>
                      ))}
                    </div>
                    {localGlobalCaption && (
                      <div className="text-sm text-gray-600 dark:text-gray-400 text-center pt-2 border-t border-gray-200 dark:border-gray-700">
                        {localGlobalCaption}
                      </div>
                    )}
                  </div>
                ) : (
                  <div className="flex items-center justify-center h-full text-gray-500 dark:text-gray-400">
                    <div className="text-center">
                      <Grid className="h-12 w-12 mx-auto mb-2 opacity-50" />
                      <p>No items in gallery</p>
                      <p className="text-sm">Add images in the Media tab</p>
                    </div>
                  </div>
                )
              ) : (
                <MediaPreview data={localData} />
              )}
            </div>
          </div>
        </div>
    </BlockEditorShell>
  )
}
