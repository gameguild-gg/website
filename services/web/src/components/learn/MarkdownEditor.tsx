import React from 'react';
import ReactMde from 'react-mde';
import ReactMarkdown from 'react-markdown';
import 'react-mde/lib/styles/css/react-mde-all.css';

interface MarkdownEditorProps {
  value: string;
  onChange: (value: string) => void;
  mode: 'light' | 'dark' | 'high-contrast';
}

const MarkdownEditor: React.FC<MarkdownEditorProps> = ({ value, onChange, mode }) => {
  const [selectedTab, setSelectedTab] = React.useState<"write" | "preview">("write");

  return (
    <div className={`markdown-editor ${mode}`}>
      <ReactMde
        value={value}
        onChange={onChange}
        selectedTab={selectedTab}
        onTabChange={setSelectedTab}
        generateMarkdownPreview={(markdown) =>
          Promise.resolve(<ReactMarkdown>{markdown}</ReactMarkdown>)
        }
      />
      <style jsx global>{`
        .markdown-editor.dark .react-mde,
        .markdown-editor.dark .mde-header {
          background-color: #2d3748;
          color: #e2e8f0;
        }
        .markdown-editor.dark .mde-textarea-wrapper textarea {
          background-color: #1a202c;
          color: #e2e8f0;
        }
        .markdown-editor.high-contrast .react-mde,
        .markdown-editor.high-contrast .mde-header {
          background-color: #000;
          color: #ffff00;
        }
        .markdown-editor.high-contrast .mde-textarea-wrapper textarea {
          background-color: #000;
          color: #ffff00;
        }
      `}</style>
    </div>
  );
};

export default MarkdownEditor;

