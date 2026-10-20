"use client"

import { useState } from "react"
import type { SerializedAudioNode } from "../../nodes/audio-node"

export function PreviewAudio({ node }: { node: SerializedAudioNode }) {
  const [hasError, setHasError] = useState(false)

  if (!node?.data) {
    console.error("Invalid audio node structure:", node)
    return null
  }

  const { src, title, artist, caption, size = 100 } = node.data

  // Verificar se é um áudio incorporado
  const getAudioEmbedInfo = (url: string): { type: string; id: string } | null => {
    if (!url) return null
    
    // YouTube
    const youtubeRegex = /(?:youtube\.com\/(?:[^/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^"&?/\s]{11})/i
    const youtubeMatch = url.match(youtubeRegex)
    if (youtubeMatch && youtubeMatch[1]) {
      return { type: "youtube", id: youtubeMatch[1] }
    }

    // Spotify
    const spotifyRegex = /(?:spotify\.com\/track\/|spotify:track:)([a-zA-Z0-9]+)/i
    const spotifyMatch = url.match(spotifyRegex)
    if (spotifyMatch && spotifyMatch[1]) {
      return { type: "spotify", id: spotifyMatch[1] }
    }

    // SoundCloud
    const soundcloudRegex = /soundcloud\.com\/([^/]+\/[^/]+)/i
    const soundcloudMatch = url.match(soundcloudRegex)
    if (soundcloudMatch && soundcloudMatch[1]) {
      return { type: "soundcloud", id: soundcloudMatch[1] }
    }

    return null
  }

  const embedInfo = getAudioEmbedInfo(src)

  // Extrair nome do arquivo de áudio da URL
  const getAudioTitle = () => {
    if (title) return title

    try {
      const url = new URL(src)
      const pathSegments = url.pathname.split("/")
      const filename = pathSegments[pathSegments.length - 1] ?? ''
      return decodeURIComponent(filename.split(".")[0] ?? 'Audio')
    } catch (e) {
      return "Audio"
    }
  }

  // Renderizar áudio incorporado
  if (embedInfo) {
    let embedUrl = ""
    let embedTitle = ""
    let height = "80"
    let className = "w-full rounded-lg border"

    switch (embedInfo.type) {
      case "youtube":
        // Adicionar parâmetros para ocultar o vídeo e mostrar apenas os controles
        // Adicionando vq=small para forçar qualidade 144p e economizar recursos
        embedUrl = `https://www.youtube-nocookie.com/embed/${embedInfo.id}?feature=oembed&enablejsapi=1&showinfo=0&controls=1&disablekb=1&rel=0&modestbranding=1&vq=small&iv_load_policy=3&fs=0`
        embedTitle = "YouTube audio player"
        height = "60" // Altura reduzida para mostrar apenas os controles
        className = "w-full rounded-lg border youtube-audio-embed" // Classe especial para estilização
        break
      case "spotify":
        embedUrl = `https://open.spotify.com/embed/track/${embedInfo.id}`
        embedTitle = "Spotify audio player"
        height = "80"
        break
      case "soundcloud":
        embedUrl = `https://w.soundcloud.com/player/?url=https%3A//soundcloud.com/${embedInfo.id}&color=%23ff5500&auto_play=false&hide_related=false&show_comments=true&show_user=true&show_reposts=false&show_teaser=true`
        embedTitle = "SoundCloud audio player"
        height = "166"
        break
      default:
        return null
    }

    return (
      <div className="my-4 relative" style={{ width: `${size}%`, margin: "0 auto" }}>
        <iframe
          src={embedUrl}
          title={embedTitle}
          className={className}
          height={height}
          allow="autoplay; clipboard-write; encrypted-media; fullscreen; picture-in-picture"
          loading="lazy"
          // @ts-expect-error credentialless is not yet in React's iframe types
          credentialless="true"
        ></iframe>
        {caption && <div className="mt-2 text-sm text-muted-foreground text-center">{caption}</div>}
      </div>
    )
  }

  // Renderizar áudio nativo
  return (
    <div className="my-4 relative">
      <div className="w-full rounded-lg border bg-card p-4" style={{ width: `${size}%`, margin: "0 auto" }}>
        <div className="flex flex-col gap-2">
          <div className="flex items-center justify-between">
            <div className="font-medium truncate">{getAudioTitle()}</div>
            {artist && <div className="text-sm text-muted-foreground">{artist}</div>}
          </div>

          <audio src={src} controls className="w-full" preload="metadata" onError={() => setHasError(true)} />
        </div>
      </div>
      {caption && <div className="mt-2 text-sm text-muted-foreground text-center">{caption}</div>}
    </div>
  )
}
