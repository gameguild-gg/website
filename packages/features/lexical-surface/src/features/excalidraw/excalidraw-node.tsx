/**
 * ExcalidrawNode — `DecoratorNode` storing the serialized scene
 * (`{appState, elements, files}` JSON string) plus optional width/height.
 *
 * Ported from `lexical-playground/src/nodes/ExcalidrawNode/index.tsx`.
 */
import * as React from "react";
import { DecoratorNode } from "lexical";
import type {
  DOMConversionMap,
  DOMConversionOutput,
  DOMExportOutput,
  EditorConfig,
  LexicalEditor,
  LexicalNode,
  NodeKey,
  SerializedLexicalNode,
  Spread,
} from "lexical";

type Dimension = number | "inherit";

const ExcalidrawComponent = React.lazy(() => import("./excalidraw-component"));

export type SerializedExcalidrawNode = Spread<
  {
    data: string;
    width?: Dimension;
    height?: Dimension;
  },
  SerializedLexicalNode
>;

function $convertExcalidrawElement(
  domNode: HTMLElement,
): DOMConversionOutput | null {
  const excalidrawData = domNode.getAttribute("data-lexical-excalidraw-json");
  const styles = window.getComputedStyle(domNode);
  const heightStr = styles.getPropertyValue("height");
  const widthStr = styles.getPropertyValue("width");
  const height: Dimension =
    !heightStr || heightStr === "inherit" ? "inherit" : parseInt(heightStr, 10);
  const width: Dimension =
    !widthStr || widthStr === "inherit" ? "inherit" : parseInt(widthStr, 10);
  if (excalidrawData) {
    return { node: $createExcalidrawNode(excalidrawData, width, height) };
  }
  return null;
}

export class ExcalidrawNode extends DecoratorNode<React.JSX.Element> {
  __data: string;
  __width: Dimension;
  __height: Dimension;

  static getType(): string {
    return "excalidraw";
  }

  static clone(node: ExcalidrawNode): ExcalidrawNode {
    return new ExcalidrawNode(
      node.__data,
      node.__width,
      node.__height,
      node.__key,
    );
  }

  static importJSON(serializedNode: SerializedExcalidrawNode): ExcalidrawNode {
    return new ExcalidrawNode(
      serializedNode.data,
      serializedNode.width ?? "inherit",
      serializedNode.height ?? "inherit",
    ).updateFromJSON(serializedNode);
  }

  exportJSON(): SerializedExcalidrawNode {
    return {
      ...super.exportJSON(),
      data: this.__data,
      height: this.__height === "inherit" ? undefined : this.__height,
      width: this.__width === "inherit" ? undefined : this.__width,
    };
  }

  constructor(
    data = "[]",
    width: Dimension = "inherit",
    height: Dimension = "inherit",
    key?: NodeKey,
  ) {
    super(key);
    this.__data = data;
    this.__width = width;
    this.__height = height;
  }

  createDOM(config: EditorConfig): HTMLElement {
    const span = document.createElement("span");
    const className = config.theme.image;
    if (className !== undefined) span.className = className;
    return span;
  }

  updateDOM(): false {
    return false;
  }

  static importDOM(): DOMConversionMap<HTMLSpanElement> | null {
    return {
      span: (domNode: HTMLSpanElement) => {
        if (!domNode.hasAttribute("data-lexical-excalidraw-json")) return null;
        return { conversion: $convertExcalidrawElement, priority: 1 };
      },
    };
  }

  exportDOM(editor: LexicalEditor): DOMExportOutput {
    const element = document.createElement("span");
    element.style.display = "inline-block";
    const content = editor.getElementByKey(this.getKey());
    if (content !== null) {
      const svg = content.querySelector("svg");
      if (svg !== null) element.appendChild(svg.cloneNode(true));
    }
    element.style.width =
      this.__width === "inherit" ? "inherit" : `${this.__width}px`;
    element.style.height =
      this.__height === "inherit" ? "inherit" : `${this.__height}px`;
    element.setAttribute("data-lexical-excalidraw-json", this.__data);
    return { element };
  }

  setData(data: string): this {
    const self = this.getWritable();
    self.__data = data;
    return self;
  }

  getWidth(): Dimension {
    return this.getLatest().__width;
  }

  setWidth(width: Dimension): this {
    const self = this.getWritable();
    self.__width = width;
    return self;
  }

  getHeight(): Dimension {
    return this.getLatest().__height;
  }

  setHeight(height: Dimension): this {
    const self = this.getWritable();
    self.__height = height;
    return self;
  }

  decorate(): React.JSX.Element {
    return (
      <ExcalidrawComponent
        nodeKey={this.getKey()}
        data={this.__data}
        width={this.__width}
        height={this.__height}
      />
    );
  }
}

export function $createExcalidrawNode(
  data = "[]",
  width: Dimension = "inherit",
  height: Dimension = "inherit",
): ExcalidrawNode {
  return new ExcalidrawNode(data, width, height);
}

export function $isExcalidrawNode(
  node: LexicalNode | null | undefined,
): node is ExcalidrawNode {
  return node instanceof ExcalidrawNode;
}
