import React from 'react';
import dynamic from 'next/dynamic';
import '@uiw/react-md-editor/markdown-editor.css';
import '@uiw/react-markdown-preview/markdown.css';

const MDEditor = dynamic(
  () => import('@uiw/react-md-editor').then((mod) => mod.default),
  { ssr: false }
);

interface MarkdownEditorProps {
  value: string;
  onChange: (value: string | undefined) => void;
  mode: 'light' | 'dark' | 'high-contrast';
}

const MarkdownEditor: React.FC<MarkdownEditorProps> = ({ value, onChange, mode }) => {
  const editorTheme = mode === 'light' ? 'light' : 'dark';

  return (
    <div className={`markdown-editor ${mode}`}>
      <MDEditor
        value={value}
        onChange={(val) => onChange(val)}
        preview="edit"
        height={400}
        theme={editorTheme}
      />
      <style jsx global>{`
        .markdown-editor.light .w-md-editor {
          background-color: #ffffff;
          color: #1f2937;
        }
        .markdown-editor.dark .w-md-editor {
          background-color: #1f2937;
          color: #e5e7eb;
        }
        .markdown-editor.high-contrast .w-md-editor {
          background-color: #000000;
          color: #ffff00;
        }
        .markdown-editor.light .w-md-editor-text {
          background-color: #f3f4f6;
        }
        .markdown-editor.dark .w-md-editor-text {
          background-color: #111827;
        }
        .markdown-editor.high-contrast .w-md-editor-text {
          background-color: #000000;
          color: #ffff00;
        }
        .markdown-editor.light .w-md-editor-preview {
          background-color: #ffffff;
        }
        .markdown-editor.dark .w-md-editor-preview {
          background-color: #1f2937;
        }
        .markdown-editor.high-contrast .w-md-editor-preview {
          background-color: #000000;
          color: #ffff00;
        }
      `}</style>
    </div>
  );
};

export default MarkdownEditor;

