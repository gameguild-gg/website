/**
 * CollapsibleContainerNode — root of a `<details>`-style block. Holds a
 * `CollapsibleTitleNode` and a `CollapsibleContentNode`.
 *
 * Ported (simplified) from
 * `lexical-playground/src/plugins/CollapsibleExtension/CollapsibleContainerNode`.
 */
import {
  ElementNode,
  type DOMExportOutput,
  type EditorConfig,
  type LexicalEditor,
  type LexicalNode,
  type LexicalUpdateJSON,
  type NodeKey,
  type SerializedElementNode,
  type Spread,
} from "lexical";

export type CollapsibleBorderStyle = "solid";

interface CollapsibleStyleOptions {  borderAlwaysVisible: boolean;
  borderStyle: CollapsibleBorderStyle;
  borderColor: string | null;
};

type SerializedCollapsibleContainerNode = Spread<
  {
    open: boolean;
    borderAlwaysVisible?: boolean;
    borderStyle?: CollapsibleBorderStyle;
    borderColor?: string | null;
  },
  SerializedElementNode
>;

const DEFAULT_COLLAPSIBLE_STYLE: CollapsibleStyleOptions = {
  borderAlwaysVisible: false,
  borderStyle: "solid",
  borderColor: null,
};

function isCollapsibleBorderStyle(
  value: unknown,
): value is CollapsibleBorderStyle {
  return value === "solid";
}

export class CollapsibleContainerNode extends ElementNode {
  __open: boolean;
  __borderAlwaysVisible: boolean;
  __borderStyle: CollapsibleBorderStyle;
  __borderColor: string | null;

  constructor(
    open: boolean,
    options: Partial<CollapsibleStyleOptions> = {},
    key?: NodeKey,
  ) {
    super(key);
    this.__open = open;
    this.__borderAlwaysVisible =
      options.borderAlwaysVisible ??
      DEFAULT_COLLAPSIBLE_STYLE.borderAlwaysVisible;
    this.__borderStyle =
      options.borderStyle ?? DEFAULT_COLLAPSIBLE_STYLE.borderStyle;
    this.__borderColor =
      typeof options.borderColor === "string" &&
      options.borderColor.trim() !== ""
        ? options.borderColor
        : null;
  }

  static getType(): string {
    return "collapsible-container";
  }

  static clone(node: CollapsibleContainerNode): CollapsibleContainerNode {
    return new CollapsibleContainerNode(
      node.__open,
      {
        borderAlwaysVisible: node.__borderAlwaysVisible,
        borderStyle: node.__borderStyle,
        borderColor: node.__borderColor,
      },
      node.__key,
    );
  }

  private applyBorderPresentation(dom: HTMLElement): void {
    dom.setAttribute(
      "data-collapsible-border-always",
      this.__borderAlwaysVisible ? "true" : "false",
    );
    dom.setAttribute("data-collapsible-border-style", this.__borderStyle);
    dom.style.setProperty("--collapsible-border-style", this.__borderStyle);
    if (this.__borderColor) {
      dom.style.setProperty("--collapsible-border-color", this.__borderColor);
      dom.setAttribute("data-collapsible-border-color", this.__borderColor);
    } else {
      dom.style.removeProperty("--collapsible-border-color");
      dom.removeAttribute("data-collapsible-border-color");
    }
  }

  createDOM(_config: EditorConfig, editor: LexicalEditor): HTMLElement {
    const dom = document.createElement("details");
    dom.open = this.__open;
    dom.classList.add("Collapsible__container");
    this.applyBorderPresentation(dom);
    dom.addEventListener("toggle", () => {
      const open = editor.getEditorState().read(() => this.getOpen());
      if (open !== dom.open) {
        editor.update(() => this.toggleOpen());
      }
    });
    return dom;
  }

  updateDOM(prevNode: this, dom: HTMLDetailsElement): boolean {
    if (prevNode.__open !== this.__open) {
      dom.open = this.__open;
    }
    if (
      prevNode.__borderAlwaysVisible !== this.__borderAlwaysVisible ||
      prevNode.__borderStyle !== this.__borderStyle ||
      prevNode.__borderColor !== this.__borderColor
    ) {
      this.applyBorderPresentation(dom);
    }
    return false;
  }

  static importJSON(
    serializedNode: SerializedCollapsibleContainerNode,
  ): CollapsibleContainerNode {
    return $createCollapsibleContainerNode(serializedNode.open, {
      borderAlwaysVisible: serializedNode.borderAlwaysVisible === true,
      borderStyle: isCollapsibleBorderStyle(serializedNode.borderStyle)
        ? serializedNode.borderStyle
        : DEFAULT_COLLAPSIBLE_STYLE.borderStyle,
      borderColor:
        typeof serializedNode.borderColor === "string" &&
        serializedNode.borderColor.trim() !== ""
          ? serializedNode.borderColor
          : null,
    }).updateFromJSON(serializedNode);
  }

  updateFromJSON(
    serializedNode: LexicalUpdateJSON<SerializedCollapsibleContainerNode>,
  ): this {
    const borderStyle = isCollapsibleBorderStyle(serializedNode.borderStyle)
      ? serializedNode.borderStyle
      : DEFAULT_COLLAPSIBLE_STYLE.borderStyle;
    const borderColor =
      typeof serializedNode.borderColor === "string" &&
      serializedNode.borderColor.trim() !== ""
        ? serializedNode.borderColor
        : null;

    return super
      .updateFromJSON(serializedNode)
      .setOpen(serializedNode.open)
      .setBorderAlwaysVisible(serializedNode.borderAlwaysVisible === true)
      .setBorderStyle(borderStyle)
      .setBorderColor(borderColor);
  }

  exportDOM(): DOMExportOutput {
    const element = document.createElement("details");
    element.classList.add("Collapsible__container");
    if (this.__open) element.setAttribute("open", "true");
    element.setAttribute(
      "data-collapsible-border-always",
      this.__borderAlwaysVisible ? "true" : "false",
    );
    element.setAttribute("data-collapsible-border-style", this.__borderStyle);
    element.style.setProperty("--collapsible-border-style", this.__borderStyle);
    if (this.__borderColor) {
      element.setAttribute("data-collapsible-border-color", this.__borderColor);
      element.style.setProperty(
        "--collapsible-border-color",
        this.__borderColor,
      );
    }
    return { element };
  }

  exportJSON(): SerializedCollapsibleContainerNode {
    return {
      ...super.exportJSON(),
      open: this.__open,
      borderAlwaysVisible: this.__borderAlwaysVisible,
      borderStyle: this.__borderStyle,
      borderColor: this.__borderColor,
    };
  }

  isShadowRoot(): boolean {
    return true;
  }

  setOpen(open: boolean): this {
    const writable = this.getWritable();
    writable.__open = open;
    return writable;
  }

  getOpen(): boolean {
    return this.getLatest().__open;
  }

  toggleOpen(): this {
    return this.setOpen(!this.getOpen());
  }

  getBorderAlwaysVisible(): boolean {
    return this.getLatest().__borderAlwaysVisible;
  }

  setBorderAlwaysVisible(next: boolean): this {
    const writable = this.getWritable();
    writable.__borderAlwaysVisible = next;
    return writable;
  }

  getBorderStyle(): CollapsibleBorderStyle {
    return this.getLatest().__borderStyle;
  }

  setBorderStyle(next: CollapsibleBorderStyle): this {
    const writable = this.getWritable();
    void next;
    writable.__borderStyle = "solid";
    return writable;
  }

  getBorderColor(): string | null {
    return this.getLatest().__borderColor;
  }

  setBorderColor(next: string | null): this {
    const writable = this.getWritable();
    writable.__borderColor =
      typeof next === "string" && next.trim() !== "" ? next : null;
    return writable;
  }
}

export function $createCollapsibleContainerNode(
  isOpen: boolean,
  options: Partial<CollapsibleStyleOptions> = {},
): CollapsibleContainerNode {
  return new CollapsibleContainerNode(isOpen, options);
}

export function $isCollapsibleContainerNode(
  node: LexicalNode | null | undefined,
): node is CollapsibleContainerNode {
  return node instanceof CollapsibleContainerNode;
}
