"use client"

import type React from "react"
import { useEffect } from "react"
import type { SerializedCustomListNode } from "../../nodes/custom-list-node"

interface PreviewListProps {
  node: SerializedCustomListNode & {
    __markerColor?: string
    "data-list-style-type"?: string
    style?: string
  }
  children: React.ReactNode
}

export function PreviewList({ node, children }: PreviewListProps) {
  const ListTag = node.listType === "bullet" ? "ul" : "ol"
  
  let listClass = ""
  let customStyle = {}
  let customProps = {}
  
  // Detectar cor personalizada se existe
  const markerColor = node.markerColor || node.__markerColor || "#3b82f6"
  const instanceId = `preview-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`
  
  // Injetar CSS para aplicar cor aos marcadores
  useEffect(() => {
    const styleId = `preview-instance-${instanceId}`
    // SEMPRE aplicar cor, incluindo a padrão - isso garante consistência
    if (!document.querySelector(`#${styleId}`)) {
      const style = document.createElement('style')
      style.id = styleId
      const listTypeSelector = node.listType === "bullet" ? "ul" : "ol"
      style.textContent = `
        ${listTypeSelector}[data-preview-instance="${instanceId}"] li::marker {
          color: ${markerColor} !important;
        }
        ${listTypeSelector}[data-preview-instance="${instanceId}"] li::before {
          color: ${markerColor} !important;
        }
        ${listTypeSelector}[data-preview-instance="${instanceId}"] li {
          color: inherit;
        }
      `
      document.head.appendChild(style)
      
      // Cleanup quando o componente for desmontado
      return () => {
        const existingStyle = document.querySelector(`#${styleId}`)
        if (existingStyle) {
          existingStyle.remove()
        }
      }
    }
  }, [instanceId, markerColor, node.listType])
  
  if (node.listType === "bullet") {
    // Para listas não ordenadas (bullet)
    const listStyleType = node.listStyleType || 
                         node['data-list-style-type'] ||
                         'disc'
                         
    switch (listStyleType) {
      case "disc":
        listClass = "list-disc list-inside"
        break
      case "circle":
        customStyle = { listStyleType: 'circle' }
        listClass = "list-inside"
        break
      case "square":
        customStyle = { listStyleType: 'square' }
        listClass = "list-inside"
        break
      case "arrow":
        listClass = "arrow-list my-4"
        customProps = { 'data-arrow-list': 'true' }
        break
      case "star":
        listClass = "star-list my-4"
        customProps = { 'data-star-list': 'true' }
        break
      default:
        listClass = "list-disc list-inside"
        break
    }
  } else {
    // Para listas ordenadas
    const listStyleType = node.listStyleType || 
                         node['data-list-style-type'] || 
                         (node.style && node.style.includes('upper-alpha') ? 'upper-alpha' :
                          node.style && node.style.includes('lower-alpha') ? 'lower-alpha' : 'decimal')
    
    listClass = "list-inside"
    
    switch (listStyleType) {
      case "upper-alpha":
        customStyle = { listStyleType: 'upper-alpha' }
        break
      case "lower-alpha":
        customStyle = { listStyleType: 'lower-alpha' }
        break
      case "upper-roman":
        customStyle = { listStyleType: 'upper-roman' }
        break
      case "lower-roman":
        customStyle = { listStyleType: 'lower-roman' }
        break
      case "decimal-leading-zero":
        customStyle = { listStyleType: 'decimal-leading-zero' }
        break
      case "decimal":
      default:
        customStyle = { listStyleType: 'decimal' }
        break
    }
  }

  // Adicionar props para aplicar cores
  customProps = { 
    ...customProps, 
    'data-preview-instance': instanceId,
    'data-marker-color': markerColor
  }

  return (
    <ListTag 
      className={`${listClass} my-4`}
      style={customStyle}
      {...customProps}
    >
      {children}
    </ListTag>
  )
}
