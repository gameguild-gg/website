/**
 * Security: rehype-raw passes raw HTML from the markdown content into the
 * hast tree verbatim. Course/lesson/blog bodies originate from server-stored
 * API content (see lesson-viewer body.html), so script-bearing markup must
 * never reach React's raw rendering path.
 *
 * This is a plugin-compatible rehype transformer (unified): a plain function
 * on the tree. It removes executable-content nodes and strips event-handler
 * attributes plus javascript:/vbscript:/data:text/html URLs from the tree
 * that rehype-raw produced.
 *
 * Types are structural (hast is not a direct dependency) — the transformer
 * only reads `type`, `tagName`, `properties`, and `children`, all of which
 * exist on every hast node variant.
 */

interface HastNode {
  type: string;
  tagName?: string;
  value?: string;
  properties?: Record<string, unknown>;
  children?: HastNode[];
}

const DANGEROUS_TAGS = new Set(['script', 'iframe', 'object', 'embed', 'link', 'meta', 'base', 'form']);

const DANGEROUS_URL_SCHEMES = ['javascript:', 'vbscript:', 'data:text/html'];

function isDangerousUrl(value: string): boolean {
  const normalized = value.trim().toLowerCase();
  return DANGEROUS_URL_SCHEMES.some((scheme) => normalized.startsWith(scheme));
}

function sanitizeNode(node: HastNode): HastNode | undefined {
  if (node.type === 'element' || node.type === 'root') {
    if (node.type === 'element' && node.tagName !== undefined && DANGEROUS_TAGS.has(node.tagName)) {
      return undefined;
    }

    if (node.properties) {
      for (const name of Object.keys(node.properties)) {
        if (name.startsWith('on')) {
          delete node.properties[name];
          continue;
        }
        const value = node.properties[name];
        if (typeof value === 'string' && isDangerousUrl(value)) {
          delete node.properties[name];
        }
      }
    }

    if (node.children) {
      node.children = node.children.map(sanitizeNode).filter((child): child is HastNode => child !== undefined);
    }
    return node;
  }

  if (node.type === 'comment') {
    return undefined;
  }

  return node;
}

export function sanitizeHastTree(tree: HastNode): HastNode {
  return sanitizeNode(tree) ?? { type: 'root', children: [] };
}
