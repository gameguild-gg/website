import { useState, useCallback } from 'react';
import { toast } from '@/components/ui/use-toast';
import JSZip from 'jszip';
import { CodeFile, History } from '../types/codeEditor';

export function useFileManager(initialFiles: CodeFile[], maxFiles: number, maxChars: number) {
  const [files, setFiles] = useState<CodeFile[]>(initialFiles);
  const [activeFileIndex, setActiveFileIndex] = useState(0);

  const addFile = useCallback((name: string, language: string) => {
    if (files.length >= maxFiles) {
      toast({
        title: "File limit reached",
        description: `You have reached the maximum number of files (${maxFiles}). Please delete a file before creating a new one.`,
        variant: "destructive",
      });
      return;
    }
    const newFile = {
      name,
      language,
      content: '',
      history: { past: [], future: [] }
    };
    setFiles(prevFiles => [...prevFiles, newFile]);
    setActiveFileIndex(files.length);
  }, [files, maxFiles]);

  const renameFile = useCallback((oldName: string, newName: string) => {
    setFiles(prevFiles => prevFiles.map(file =>
      file.name === oldName ? { ...file, name: newName } : file
    ));
    setActiveFileIndex(files.findIndex(file => file.name === newName));
  }, [files]);

  const deleteFile = useCallback((name: string) => {
    const fileIndex = files.findIndex(file => file.name === name);
    if (fileIndex === -1) return;

    setFiles(prevFiles => prevFiles.filter(file => file.name !== name));

    if (activeFileIndex >= files.length - 1) {
      setActiveFileIndex(files.length - 2);
    }
  }, [files, activeFileIndex]);

  const importFile = useCallback(async (file: File) => {
    if (files.length >= maxFiles) {
      toast({
        title: "File limit reached",
        description: `You have reached the maximum number of files (${maxFiles}). Please delete a file before importing a new one.`,
        variant: "destructive",
      });
      return;
    }
    try {
      const content = await file.text();
      if (content.length > maxChars) {
        toast({
          title: "Character limit exceeded",
          description: `Importing this file would exceed the maximum character limit of ${maxChars} for this file. Please reduce the content before importing.`,
          variant: "destructive",
        });
        return;
      }
      const newFile: CodeFile = {
        name: file.name,
        language: getLanguageFromExtension(file.name),
        content: content,
        history: { past: [], future: [] }
      };
      setFiles(prevFiles => [...prevFiles, newFile]);
      setActiveFileIndex(files.length);
    } catch (error) {
      console.error('Error reading file:', error);
      toast({
        title: "Error importing file",
        description: "An error occurred while importing the file. Please try again.",
        variant: "destructive",
      });
    }
  }, [files, maxFiles, maxChars]);

  const exportFiles = useCallback(async (exportAll: boolean, singleFileName?: string) => {
    const zip = new JSZip();
    const folder = zip.folder("exported_files");

    if (folder) {
      if (exportAll) {
        files.forEach(file => {
          folder.file(file.name, file.content);
        });
      } else if (singleFileName) {
        const fileToExport = files.find(file => file.name === singleFileName);
        if (fileToExport) {
          folder.file(fileToExport.name, fileToExport.content);
        }
      }

      const content = await zip.generateAsync({ type: "blob" });
      const url = window.URL.createObjectURL(content);
      const a = document.createElement('a');
      a.href = url;
      a.download = exportAll ? "all_files.zip" : `${singleFileName}.zip`;
      a.click();
      window.URL.revokeObjectURL(url);
    }
  }, [files]);

  const updateFileContent = useCallback((index: number, newContent: string) => {
    setFiles(prevFiles => {
      const updatedFiles = [...prevFiles];
      const currentFile = { ...updatedFiles[index] };
      currentFile.history.past.push(currentFile.content);
      currentFile.history.future = [];
      currentFile.content = newContent;
      updatedFiles[index] = currentFile;
      return updatedFiles;
    });
  }, []);

  const undo = useCallback(() => {
    setFiles(prevFiles => {
      const updatedFiles = [...prevFiles];
      const currentFile = { ...updatedFiles[activeFileIndex] };
      if (currentFile.history.past.length > 0) {
        const previousContent = currentFile.history.past.pop()!;
        currentFile.history.future.push(currentFile.content);
        currentFile.content = previousContent;
        updatedFiles[activeFileIndex] = currentFile;
      }
      return updatedFiles;
    });
  }, [activeFileIndex]);

  const redo = useCallback(() => {
    setFiles(prevFiles => {
      const updatedFiles = [...prevFiles];
      const currentFile = { ...updatedFiles[activeFileIndex] };
      if (currentFile.history.future.length > 0) {
        const nextContent = currentFile.history.future.pop()!;
        currentFile.history.past.push(currentFile.content);
        currentFile.content = nextContent;
        updatedFiles[activeFileIndex] = currentFile;
      }
      return updatedFiles;
    });
  }, [activeFileIndex]);

  return {
    files,
    activeFileIndex,
    setActiveFileIndex,
    addFile,
    renameFile,
    deleteFile,
    importFile,
    exportFiles,
    updateFileContent,
    undo,
    redo,
  };
}

function getLanguageFromExtension(filename: string): string {
  // Implement this function based on your language mapping
  const extension = filename.split('.').pop()?.toLowerCase();
  // Add your language mapping logic here
  return 'javascript'; // Default to JavaScript for this example
}

