import React, { useRef, useEffect, useMemo } from 'react';
import ReactQuill from 'react-quill';
import Quill from 'quill';
import 'react-quill/dist/quill.snow.css';

interface RichTextEditorProps {
  value: string;
  onChange: (value: string) => void;
  mode: 'light' | 'dark' | 'high-contrast';
}

const RichTextEditor: React.FC<RichTextEditorProps> = ({ value, onChange, mode }) => {
  const quillRef = useRef<ReactQuill | null>(null);
  const modules = useMemo(() => ({
    toolbar: [
      [{ 'header': [1, 2, 3, false] }],
      ['bold', 'italic', 'underline', 'strike'],
      [{ 'list': 'ordered'}, { 'list': 'bullet' }],
      ['link', 'image'],
      ['clean']
    ],
  }), []);

  const formats = useMemo(() => [
    'header',
    'bold', 'italic', 'underline', 'strike',
    'list', 'bullet',
    'link', 'image'
  ], []);

  useEffect(() => {
    if (quillRef.current) {
      const quill: Quill = quillRef.current.getEditor();

      // Apply theme-specific styles
      if (mode === 'dark') {
        quill.root.style.backgroundColor = '#2d3748';
        quill.root.style.color = '#e2e8f0';
      } else if (mode === 'high-contrast') {
        quill.root.style.backgroundColor = '#000';
        quill.root.style.color = '#ffff00';
      }
    }
  }, [mode]);

  return (
    <div className={`rich-text-editor ${mode}`}>
      <ReactQuill
        theme="snow"
        ref={quillRef} // Assign ref to the ReactQuill component
        value={value}
        onChange={onChange}
        modules={modules}
        formats={formats}
      />
    </div>
  );
};

export default RichTextEditor;

