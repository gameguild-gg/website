"use client"

import { ListNode, SerializedListNode, ListType } from "@lexical/list"
import type { LexicalNode, NodeKey, EditorConfig, SerializedLexicalNode } from "lexical"

export interface SerializedCustomListNode extends SerializedListNode {
  listStyleType?: string
  markerColor?: string
}

export class CustomListNode extends ListNode {
  __listStyleType: string
  __markerColor: string

  static getType(): string {
    // Tipo próprio (distinto de "list") para evitar conflito de
    // type/klass com o ListNode padrão no registry do Lexical.
    return "custom-list"
  }

  constructor(listType: ListType, start: number, listStyleType?: string, markerColor?: string, key?: NodeKey) {
    super(listType, start, key)
    this.__listStyleType = listStyleType || "decimal"
    this.__markerColor = markerColor || "#3b82f6" // Sempre definir cor padrão
  }

  getListStyleType(): string {
    return this.__listStyleType
  }

  setListStyleType(listStyleType: string): void {
    const writable = this.getWritable()
    writable.__listStyleType = listStyleType
  }

  getMarkerColor(): string {
    return this.__markerColor
  }

  setMarkerColor(markerColor: string): void {
    const writable = this.getWritable()
    writable.__markerColor = markerColor
  }

  static clone(node: CustomListNode): CustomListNode {
    return new CustomListNode(
      node.getListType(),
      node.getStart(),
      node.__listStyleType,
      node.__markerColor,
      node.__key
    )
  }

  createDOM(config: EditorConfig): HTMLElement {
    const element = super.createDOM(config)
    
    // Limpar todas as classes CSS do tema
    element.className = ""
    
    // Aplicar estilos baseado no tipo de lista (tanto ordered quanto unordered)
    this.applyListStyles(element)
    
    // Definir atributo apropriado baseado no tipo
    if (this.getListType() === "number") {
      element.setAttribute("data-list-style-type", this.__listStyleType || "decimal")
    } else {
      element.setAttribute("data-list-style-type", this.__listStyleType || "disc")
    }
    
    // IMPORTANTE: Aplicar cor imediatamente após a criação do DOM
    this.applyMarkerColor(element)
    
    return element
  }

  updateDOM(prevNode: CustomListNode, dom: HTMLElement, config: EditorConfig): boolean {
    const result = super.updateDOM(prevNode as this, dom, config)
    
    // Limpar todas as classes CSS do tema
    dom.className = ""
    
    // Aplicar estilos baseado no tipo de lista (tanto ordered quanto unordered)
    this.applyListStyles(dom)
    
    // Definir atributo apropriado baseado no tipo
    if (this.getListType() === "number") {
      dom.setAttribute("data-list-style-type", this.__listStyleType || "decimal")
    } else {
      dom.setAttribute("data-list-style-type", this.__listStyleType || "disc")
    }
    
    // IMPORTANTE: Reaplicar cor após atualização do DOM
    this.applyMarkerColor(dom)
    
    return result
  }

  private applyMarkerColor(element: HTMLElement): void {
    // Aplicar cor especificamente aos marcadores após o DOM estar pronto
    const listType = this.__listStyleType || "decimal"
    const markerColor = this.__markerColor || "#3b82f6"
    
    // Remover instância anterior se existir
    const oldInstanceId = element.getAttribute("data-list-instance")
    if (oldInstanceId) {
      // Remover estilos antigos
      const oldStyle = document.querySelector(`#instance-${oldInstanceId}`)
      if (oldStyle) {
        oldStyle.remove()
      }
    }
    
    // Adicionar um identificador único para essa instância específica
    const instanceId = `list-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`
    element.setAttribute("data-list-instance", instanceId)
    
    // SEMPRE aplicar estilos de cor, incluindo a cor padrão
    // Isso garante que a deserialização funcione corretamente
    switch (listType) {
      case "decimal":
      case "upper-alpha":
      case "lower-alpha":
      case "upper-roman":
      case "lower-roman":
      case "decimal-leading-zero":
      case "disc":
      case "circle":
      case "square":
        this.addStandardListMarkerStyles(element, listType, markerColor)
        break
      case "greek-upper":
        this.addGreekNumberStyles(element, markerColor)
        break
      case "circled":
        this.addCircledNumberStyles(element, markerColor)
        break
      case "arrow":
        this.addArrowListStyles(element, markerColor)
        break
      case "star":
        this.addStarListStyles(element, markerColor)
        break
    }
  }

  private applyListStyles(element: HTMLElement): void {
    const listType = this.__listStyleType || "decimal"
    const markerColor = this.__markerColor || "#3b82f6"
    
    // Estilos básicos para todas as listas
    element.style.listStylePosition = "inside"
    element.style.paddingLeft = "1rem"
    element.style.marginTop = "1rem"
    element.style.marginBottom = "1rem"
    
    // IMPORTANTE: NÃO aplicar cor ao elemento da lista inteira
    // A cor será aplicada apenas aos marcadores através de CSS
    
    // Estilos específicos por tipo
    switch (listType) {
      case "decimal":
      case "upper-alpha":
      case "lower-alpha":
      case "upper-roman":
      case "lower-roman":
      case "decimal-leading-zero":
        element.style.listStyleType = listType
        // Para listas padrão, aplicar cor apenas aos marcadores via CSS
        this.addStandardListMarkerStyles(element, listType, markerColor)
        break
      case "greek-upper":
        // Numeração grega customizada
        element.style.listStyleType = "none"
        element.style.counterReset = "greek-counter"
        this.addGreekNumberStyles(element, markerColor)
        break
      case "circled":
        element.style.listStyleType = "none"
        element.style.counterReset = "circled-counter"
        this.addCircledNumberStyles(element, markerColor)
        break
      // Estilos para listas não ordenadas (bullet)
      case "disc":
      case "circle":
      case "square":
        element.style.listStyleType = listType
        // Para listas padrão, aplicar cor apenas aos marcadores via CSS
        this.addStandardListMarkerStyles(element, listType, markerColor)
        break
      case "arrow":
        element.style.listStyleType = "none"
        element.setAttribute("data-arrow-list", "true")
        this.addArrowListStyles(element, markerColor)
        break
      case "star":
        element.style.listStyleType = "none"
        element.setAttribute("data-star-list", "true")
        this.addStarListStyles(element, markerColor)
        break
      default:
        // Determinar estilo padrão baseado no tipo de lista
        if (this.getListType() === "bullet") {
          element.style.listStyleType = "disc"
          this.addStandardListMarkerStyles(element, "disc", markerColor)
        } else {
          element.style.listStyleType = "decimal"
          this.addStandardListMarkerStyles(element, "decimal", markerColor)
        }
    }
  }

  private addStandardListMarkerStyles(element: HTMLElement, listType: string, markerColor: string): void {
    const instanceId = element.getAttribute("data-list-instance")
    
    if (instanceId) {
      const specificStyleId = `instance-${instanceId}`
      // Remover estilo anterior se existir
      const oldStyle = document.querySelector(`#${specificStyleId}`)
      if (oldStyle) {
        oldStyle.remove()
      }
      
      const instanceStyle = document.createElement('style')
      instanceStyle.id = specificStyleId
      const listTag = this.getListType() === "bullet" ? "ul" : "ol"
      
      // Criar seletores mais específicos e poderosos
      instanceStyle.textContent = `
        /* Seletores específicos para a instância */
        ${listTag}[data-list-instance="${instanceId}"] {
          list-style-type: ${listType};
        }
        ${listTag}[data-list-instance="${instanceId}"] li::marker {
          color: ${markerColor} !important;
          font-weight: inherit;
        }
        ${listTag}[data-list-instance="${instanceId}"] li {
          color: inherit !important;
        }
        
        /* Seletores para o editor Lexical */
        .lexical-editor ${listTag}[data-list-instance="${instanceId}"] li::marker {
          color: ${markerColor} !important;
        }
        .lexical-editor ${listTag}[data-list-instance="${instanceId}"] li {
          color: inherit !important;
        }
        
        /* Fallback genérico */
        ${listTag}[data-list-style-type="${listType}"][data-list-instance="${instanceId}"] li::marker {
          color: ${markerColor} !important;
        }
      `
      document.head.appendChild(instanceStyle)
    }
  }

  private addGreekNumberStyles(element: HTMLElement, markerColor: string): void {
    // Criar estilos CSS para numeração grega se ainda não existirem
    const styleId = `greek-number-${markerColor.replace(/[^\w]/g, '')}`
    const instanceId = element.getAttribute("data-list-instance")
    
    if (!document.querySelector(`#${styleId}`)) {
      const style = document.createElement('style')
      style.id = styleId
      style.textContent = `
        ol[data-list-style-type="greek-upper"] {
          counter-reset: greek-counter;
        }
        ol[data-list-style-type="greek-upper"] li::before {
          counter-increment: greek-counter;
          content: attr(data-greek-number);
          font-weight: bold;
          color: ${markerColor} !important;
          margin-right: 0.5rem;
          display: inline-block;
        }
        ol[data-list-style-type="greek-upper"] li {
          color: inherit; /* Manter cor do texto normal */
        }
      `
      document.head.appendChild(style)
    }
    
    // Aplicar estilo específico para a instância
    if (instanceId) {
      const specificStyleId = `greek-instance-${instanceId}`
      if (!document.querySelector(`#${specificStyleId}`)) {
        const instanceStyle = document.createElement('style')
        instanceStyle.id = specificStyleId
        instanceStyle.textContent = `
          ol[data-list-instance="${instanceId}"] li::before {
            counter-increment: greek-counter;
            content: attr(data-greek-number);
            font-weight: bold;
            color: ${markerColor} !important;
            margin-right: 0.5rem;
            display: inline-block;
          }
          ol[data-list-instance="${instanceId}"] {
            counter-reset: greek-counter;
          }
          ol[data-list-instance="${instanceId}"] li {
            color: inherit;
          }
        `
        document.head.appendChild(instanceStyle)
      }
    }
  }

  private addCircledNumberStyles(element: HTMLElement, markerColor: string): void {
    // Criar estilos CSS para números circulados se ainda não existirem
    const styleId = `circled-number-${markerColor.replace(/[^\w]/g, '')}`
    const instanceId = element.getAttribute("data-list-instance")
    
    if (!document.querySelector(`#${styleId}`)) {
      const style = document.createElement('style')
      style.id = styleId
      style.textContent = `
        ol[data-list-style-type="circled"] {
          counter-reset: circled-counter;
        }
        ol[data-list-style-type="circled"] li::before {
          counter-increment: circled-counter;
          content: "(" counter(circled-counter) ")";
          font-weight: bold;
          color: ${markerColor} !important;
          margin-right: 0.5rem;
          display: inline-block;
        }
        ol[data-list-style-type="circled"] li {
          color: inherit; /* Manter cor do texto normal */
        }
      `
      document.head.appendChild(style)
    }
    
    // Aplicar estilo específico para a instância
    if (instanceId) {
      const specificStyleId = `circled-instance-${instanceId}`
      if (!document.querySelector(`#${specificStyleId}`)) {
        const instanceStyle = document.createElement('style')
        instanceStyle.id = specificStyleId
        instanceStyle.textContent = `
          ol[data-list-instance="${instanceId}"] li::before {
            counter-increment: circled-counter;
            content: "(" counter(circled-counter) ")";
            font-weight: bold;
            color: ${markerColor} !important;
            margin-right: 0.5rem;
            display: inline-block;
          }
          ol[data-list-instance="${instanceId}"] {
            counter-reset: circled-counter;
          }
          ol[data-list-instance="${instanceId}"] li {
            color: inherit;
          }
        `
        document.head.appendChild(instanceStyle)
      }
    }
  }

  private addArrowListStyles(element: HTMLElement, markerColor: string): void {
    const instanceId = element.getAttribute("data-list-instance")
    
    if (instanceId) {
      const specificStyleId = `arrow-instance-${instanceId}`
      // Remover estilo anterior se existir
      const oldStyle = document.querySelector(`#${specificStyleId}`)
      if (oldStyle) {
        oldStyle.remove()
      }
      
      const instanceStyle = document.createElement('style')
      instanceStyle.id = specificStyleId
      const listTag = this.getListType() === "bullet" ? "ul" : "ol"
      
      instanceStyle.textContent = `
        ${listTag}[data-list-instance="${instanceId}"] {
          list-style: none;
        }
        ${listTag}[data-list-instance="${instanceId}"] li::before {
          content: "▶";
          font-weight: bold;
          color: ${markerColor} !important;
          margin-right: 0.5rem;
          display: inline-block;
        }
        ${listTag}[data-list-instance="${instanceId}"] li {
          color: inherit !important;
        }
        .lexical-editor ${listTag}[data-list-instance="${instanceId}"] li::before {
          color: ${markerColor} !important;
        }
      `
      document.head.appendChild(instanceStyle)
    }
  }

  private addStarListStyles(element: HTMLElement, markerColor: string): void {
    const instanceId = element.getAttribute("data-list-instance")
    
    if (instanceId) {
      const specificStyleId = `star-instance-${instanceId}`
      // Remover estilo anterior se existir
      const oldStyle = document.querySelector(`#${specificStyleId}`)
      if (oldStyle) {
        oldStyle.remove()
      }
      
      const instanceStyle = document.createElement('style')
      instanceStyle.id = specificStyleId
      const listTag = this.getListType() === "bullet" ? "ul" : "ol"
      
      instanceStyle.textContent = `
        ${listTag}[data-list-instance="${instanceId}"] {
          list-style: none;
        }
        ${listTag}[data-list-instance="${instanceId}"] li::before {
          content: "★";
          font-weight: bold;
          color: ${markerColor} !important;
          margin-right: 0.5rem;
          display: inline-block;
        }
        ${listTag}[data-list-instance="${instanceId}"] li {
          color: inherit !important;
        }
        .lexical-editor ${listTag}[data-list-instance="${instanceId}"] li::before {
          color: ${markerColor} !important;
        }
      `
      document.head.appendChild(instanceStyle)
    }
  }

  static importJSON(serializedNode: SerializedLexicalNode & Record<string, unknown>): CustomListNode {
    const payload = serializedNode as Partial<SerializedCustomListNode>
    const listType = payload.listType ?? "number"
    const start = payload.start ?? 1
    const listStyleType = payload.listStyleType
    const markerColor = payload.markerColor
    const node = new CustomListNode(listType as ListType, start, listStyleType, markerColor)
    
    // IMPORTANTE: Sempre definir a cor, incluindo a padrão
    // Se não há cor salva, usar a padrão
    const finalColor = markerColor || "#3b82f6"
    node.__markerColor = finalColor
    
    return node
  }

  exportJSON(): SerializedCustomListNode {
    return {
      ...super.exportJSON(),
      listStyleType: this.__listStyleType,
      markerColor: this.__markerColor || "#3b82f6",
      type: "custom-list",
    }
  }
}

export function $createCustomListNode(
  listType: ListType,
  start = 1,
  listStyleType = "decimal",
  markerColor = "#3b82f6" // Sempre definir cor padrão
): CustomListNode {
  return new CustomListNode(listType, start, listStyleType, markerColor)
}

export function $isCustomListNode(
  node: LexicalNode | null | undefined
): node is CustomListNode {
  return node instanceof CustomListNode
}
