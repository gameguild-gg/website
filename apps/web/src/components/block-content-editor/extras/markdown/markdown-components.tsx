import { type Components } from "react-markdown"
import { Prism as SyntaxHighlighter } from "react-syntax-highlighter"
import { vscDarkPlus, vs } from "react-syntax-highlighter/dist/esm/styles/prism"
import { useTheme } from "next-themes"
import { MermaidDiagram } from "@game-guild/content-rendering"
import { VegaLiteViewer } from "@game-guild/lexical-surface"

export function useMarkdownComponents(): Components {
  const { theme } = useTheme()
  const isDarkMode = theme === "dark"

  return {
    // Code blocks with syntax highlighting
    code({ node, className, children, ...props }: any) {
      const match = /language-(\w[\w-]*)/.exec(className || "")
      const language = match ? match[1] : ""
      const inline = !className

      if (!inline && language === "mermaid") {
        return <MermaidDiagram code={String(children).replace(/\n$/, "")} />
      }

      if (!inline && (language === "vegalite" || language === "vega-lite")) {
        return <VegaLiteViewer spec={String(children).replace(/\n$/, "")} />
      }

      return !inline && language ? (
        <SyntaxHighlighter
          style={isDarkMode ? (vscDarkPlus as any) : (vs as any)}
          language={language}
          PreTag="div"
          className="mt-4! mb-4! rounded-lg! text-sm!"
          {...props}
        >
          {String(children).replace(/\n$/, "")}
        </SyntaxHighlighter>
      ) : (
        <code
          className="px-1.5 py-0.5 rounded bg-gray-100 dark:bg-gray-800 text-pink-600 dark:text-pink-400 font-mono text-sm"
          {...props}
        >
          {children}
        </code>
      )
    },

    // Headings with proper styling
    h1: ({ children, ...props }) => (
      <h1 className="text-4xl font-bold mt-8 mb-4 text-gray-900 dark:text-gray-100" {...props}>
        {children}
      </h1>
    ),
    h2: ({ children, ...props }) => (
      <h2 className="text-3xl font-bold mt-6 mb-3 text-gray-900 dark:text-gray-100" {...props}>
        {children}
      </h2>
    ),
    h3: ({ children, ...props }) => (
      <h3 className="text-2xl font-bold mt-5 mb-2 text-gray-900 dark:text-gray-100" {...props}>
        {children}
      </h3>
    ),
    h4: ({ children, ...props }) => (
      <h4 className="text-xl font-bold mt-4 mb-2 text-gray-900 dark:text-gray-100" {...props}>
        {children}
      </h4>
    ),
    h5: ({ children, ...props }) => (
      <h5 className="text-lg font-bold mt-3 mb-2 text-gray-900 dark:text-gray-100" {...props}>
        {children}
      </h5>
    ),
    h6: ({ children, ...props }) => (
      <h6 className="text-base font-bold mt-2 mb-2 text-gray-900 dark:text-gray-100" {...props}>
        {children}
      </h6>
    ),

    // Paragraphs
    p: ({ children, ...props }) => (
      <p className="mb-4 leading-7 text-gray-700 dark:text-gray-300" {...props}>
        {children}
      </p>
    ),

    // Links
    a: ({ children, href, ...props }) => (
      <a
        href={href}
        className="text-blue-600 dark:text-blue-400 hover:underline"
        target="_blank"
        rel="noopener noreferrer"
        {...props}
      >
        {children}
      </a>
    ),

    // Blockquotes
    blockquote: ({ children, ...props }) => (
      <blockquote
        className="border-l-4 border-blue-500 bg-blue-50 dark:bg-blue-900/10 pl-4 pr-4 py-2 my-4 italic"
        {...props}
      >
        {children}
      </blockquote>
    ),

    // Lists
    ul: ({ children, ...props }) => (
      <ul className="list-disc list-inside mb-4 space-y-2 text-gray-700 dark:text-gray-300" {...props}>
        {children}
      </ul>
    ),
    ol: ({ children, ...props }) => (
      <ol className="list-decimal list-inside mb-4 space-y-2 text-gray-700 dark:text-gray-300" {...props}>
        {children}
      </ol>
    ),
    li: ({ children, ...props }) => {
      // Check if this is a task list item
      const childArray = Array.isArray(children) ? children : [children]
      const firstChild = childArray[0]
      
      if (typeof firstChild === "string") {
        // Task list item with [ ] or [x]
        const taskMatch = firstChild.match(/^\[([ x])\]\s*(.*)/)
        if (taskMatch) {
          const [, checked, text] = taskMatch
          const isChecked = checked === "x"
          const remainingChildren = childArray.slice(1)
          
          return (
            <li className="flex items-start gap-2 list-none" {...props}>
              <input
                type="checkbox"
                checked={isChecked}
                readOnly
                className="mt-1 h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
              />
              <span className={isChecked ? "line-through text-gray-500 dark:text-gray-500" : ""}>
                {text}
                {remainingChildren}
              </span>
            </li>
          )
        }
      }

      return (
        <li className="text-gray-700 dark:text-gray-300" {...props}>
          {children}
        </li>
      )
    },

    // Tables
    table: ({ children, ...props }) => (
      <div className="overflow-x-auto my-4">
        <table className="min-w-full border-collapse border border-gray-300 dark:border-gray-600" {...props}>
          {children}
        </table>
      </div>
    ),
    thead: ({ children, ...props }) => (
      <thead className="bg-gray-100 dark:bg-gray-800" {...props}>
        {children}
      </thead>
    ),
    tbody: ({ children, ...props }) => (
      <tbody {...props}>
        {children}
      </tbody>
    ),
    tr: ({ children, ...props }) => (
      <tr className="border-b border-gray-200 dark:border-gray-700" {...props}>
        {children}
      </tr>
    ),
    th: ({ children, ...props }) => (
      <th
        className="px-4 py-2 text-left font-semibold text-gray-900 dark:text-gray-100 border border-gray-300 dark:border-gray-600"
        {...props}
      >
        {children}
      </th>
    ),
    td: ({ children, ...props }) => (
      <td
        className="px-4 py-2 text-gray-700 dark:text-gray-300 border border-gray-300 dark:border-gray-600"
        {...props}
      >
        {children}
      </td>
    ),

    // Horizontal rule
    hr: ({ ...props }) => (
      <hr className="my-6 border-gray-300 dark:border-gray-700" {...props} />
    ),

    // Images
    img: ({ src, alt, ...props }) => (
      <img
        src={src}
        alt={alt}
        className="rounded-lg shadow-md max-w-full h-auto my-4"
        {...props}
      />
    ),

    // Strong/Bold
    strong: ({ children, ...props }) => (
      <strong className="font-bold text-gray-900 dark:text-gray-100" {...props}>
        {children}
      </strong>
    ),

    // Emphasis/Italic
    em: ({ children, ...props }) => (
      <em className="italic text-gray-800 dark:text-gray-200" {...props}>
        {children}
      </em>
    ),

    // Delete/Strikethrough
    del: ({ children, ...props }) => (
      <del className="line-through text-gray-500 dark:text-gray-500" {...props}>
        {children}
      </del>
    ),

    // Details/Summary (Collapsible)
    details: ({ children, ...props }) => (
      <details className="my-4 p-4 border border-gray-300 dark:border-gray-600 rounded-lg bg-gray-50 dark:bg-gray-800/50" {...props}>
        {children}
      </details>
    ),
    summary: ({ children, ...props }) => (
      <summary className="font-semibold text-gray-900 dark:text-gray-100 cursor-pointer hover:text-blue-600 dark:hover:text-blue-400 mb-2" {...props}>
        {children}
      </summary>
    ),
  }
}
