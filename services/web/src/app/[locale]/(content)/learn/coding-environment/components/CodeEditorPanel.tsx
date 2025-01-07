'use client'

import { useState, useEffect, useRef, useCallback } from 'react'
import dynamic from 'next/dynamic'
import { CodeQuestionv1_0_0 } from '@/lib/interface-base/question.base.v1.0.0'
import { QuestionSubmissionv1_0_0 } from '@/lib/interface-base/question.submission.v1.0.0'
import TopMenu from './TopMenu'
import BottomMenu from './BottomMenu'
import CodeTabs from './CodeTabs'
import TestResultsTab from './TestResultsTab'
import LessonPanel from './LessonPanel'
import { CharacterCount } from './CharacterCount'
import { Panel, PanelGroup, PanelResizeHandle, ImperativePanelHandle } from 'react-resizable-panels'
import JSZip from 'jszip';
import { toast } from '@/components/learn/ui/use-toast'
import { SubmissionWarningDialog } from './SubmissionWarningDialog'
import { History, CodeFile } from '../types/codeEditor';
import { Undo, Redo, AlertCircle } from 'lucide-react';
//import * as monaco from 'monaco-editor';
import SettingsModal, { Settings } from './SettingsModal'
//import { EditorPanel } from './EditorPanel'
//import { loader } from '@monaco-editor/react';
import { insertInputs } from './InputInserter';
import { ConfirmSubmissionDialog } from './ConfirmSubmissionDialog';
import { AbortController } from 'node-abort-controller'
import { SplitViewFileSelector } from './SplitViewFileSelector';
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { useRouter, useSearchParams } from 'next/navigation'
import { HierarchyBasev1_0_0 } from '@/lib/interface-base/structure.base.v1.0.0'
import { UserListQuestionv1_0_0, StudentSubmission } from '@/lib/interface-base/user.list.question.v1.0.0';

// Add this function after the existing imports
const getFileCharLimit = (fileName: string, rules: string[]): number => {
  const fileSpecificRule = rules.find(rule => rule.startsWith(`chars-${fileName}:`))
  if (fileSpecificRule) {
    return parseInt(fileSpecificRule.split(':')[1].trim())
  }
  return 0 // Return 0 if no specific limit is set for this file
}

const saveStateToLocalStorage = (state: any, courseId: string | null, moduleId: string | null, assessmentId: string | null) => {
  if (!courseId || !moduleId || !assessmentId || !state.codeQuestion) return; // Return early if IDs are missing

  const { id } = state.codeQuestion;
  const storageKey = `assignmentState_${courseId}_${moduleId}_${assessmentId}_${id}`;
  localStorage.setItem(storageKey, JSON.stringify(state));
};

const MonacoEditor = dynamic(() => import('@monaco-editor/react'), { ssr: false })
/*
interface CodeFile {
  name: string;
  language: string;
  content: string;
  history: History;
}
*/
interface CodeEditorPanelProps {
  codeQuestion: CodeQuestionv1_0_0
  onReturn: () => void
  onSubmit: (submission: QuestionSubmissionv1_0_0) => void
  hierarchy?: HierarchyBasev1_0_0
}

const languageMap: { [key: string]: string } = {
  js: 'javascript',
  jsx: 'javascript',
  ts: 'typescript',
  tsx: 'typescript',
  py: 'python',
  java: 'java',
  cpp: 'cpp',
  c: 'c',
  go: 'go',
  html: 'html',
  css: 'css',
  md: 'markdown',
  json: 'json',
  yaml: 'yaml',
  xml: 'xml',
  rb: 'ruby',
  rs: 'rust',
  cs: 'csharp',
  lua: 'lua'
}

const getLanguageFromExtension = (filename: string): string => {
  const extension = filename.split('.').pop()?.toLowerCase() || ''
  return languageMap[extension] || 'plaintext'
}

const DEFAULT_MAX_FILES = 6
const DEFAULT_MAX_CHARS = 500000

const defaultSettings: Settings = {
  runSettings: {
    fileExtensions: {
      '.html': 'Open new guide',
      '.js': 'Internal output',
      '.jsx': 'Internal output',
      '.ts': 'Internal output',
      '.tsx': 'Internal output',
      '.py': 'Internal output',
      '.java': 'Internal output',
      '.cpp': 'Internal output',
      '.c': 'Internal output',
      '.rb': 'Internal output',
      '.lua': 'Internal output'
    },
    applicationFileName: 'index',
  },
};

// Add this function to calculate total character count
const calculateTotalChars = (files: CodeFile[], rules: string[]): number => {
  return files.reduce((sum, file) => {
    const fileLimit = getFileCharLimit(file.name, rules)
    return fileLimit === 0 ? sum + file.content.length : sum
  }, 0)
}

export default function CodeEditorPanel({ codeQuestion, onSubmit, hierarchy = {} }: CodeEditorPanelProps) {
  const [codeFiles, setCodeFiles] = useState<CodeFile[]>([])
  const [activeFileIndex, setActiveFileIndex] = useState(0)
  const [activeTab, setActiveTab] = useState<'output' | 'problems' | 'testResults' | null>(null)
  const [output, setOutput] = useState('')
  const [testResults, setTestResults] = useState<any[]>([])
  const [mode, setMode] = useState<'light' | 'dark' | 'high-contrast'>(hierarchy.colorMode)
  const [submissionData, setSubmissionData] = useState<Partial<QuestionSubmissionv1_0_0>>({
    submittedCode: [],
    output: '',
    testResults: []
  });
  const editorRef = useRef<any>(null)
  const [currentFileChars, setCurrentFileChars] = useState(0)
  const [totalChars, setTotalChars] = useState(0)
  const [isWarningDialogOpen, setIsWarningDialogOpen] = useState(false)
  const [submissionConflicts, setSubmissionConflicts] = useState<{ fileName: string; reason: string }[]>([])
  const [isSubmitDisabled, setIsSubmitDisabled] = useState(false)
  const [lessonPanelSize, setLessonPanelSize] = useState(30)
  const [isLessonCollapsed, setIsLessonCollapsed] = useState(false)
  const [prevLessonPanelSize, setPrevLessonPanelSize] = useState(30);
  const lessonPanelRef = useRef<ImperativePanelHandle>(null)
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [settings, setSettings] = useState<Settings>(defaultSettings);
  const [isUndoing, setIsUndoing] = useState(false);
  const [isRedoing, setIsRedoing] = useState(false);
  const undoTimerRef = useRef<NodeJS.Timeout | null>(null);
  const redoTimerRef = useRef<NodeJS.Timeout | null>(null);
  const [isConfirmDialogOpen, setIsConfirmDialogOpen] = useState(false);
  const [abortController, setAbortController] = useState<AbortController | null>(null);
  const [isSplitViewActive, setIsSplitViewActive] = useState(false);
  const [splitViewFileName, setSplitViewFileName] = useState<string | null>(null);
  const [isFileSelectorOpen, setIsFileSelectorOpen] = useState(false);
  const [openedWindow, setOpenedWindow] = useState<Window | null>(null);
  const [isRunAnimating, setIsRunAnimating] = useState(false);
  const [isRunThisAnimating, setIsRunThisAnimating] = useState(false);
  const [isRunAppAnimating, setIsRunAppAnimating] = useState(false);
  const [failedTests, setFailedTests] = useState(0)
  const [totalTests, setTotalTests] = useState(0)
  const [testResultsViewed, setTestResultsViewed] = useState(false); // Added state
  const [isSubmitHidden, setIsSubmitHidden] = useState(false); // Added state
  const [splitViewFileIndex, setSplitViewFileIndex] = useState<number | null>(null); // Added state
  const router = useRouter()
  const searchParams = useSearchParams()
  const assessmentId = searchParams.get('assessmentId')
  const userId = searchParams.get('userId')
  const role = searchParams.get('role')
  const courseId = searchParams.get('courseId')
  const moduleId = searchParams.get('moduleId')
  
  const [studentSubmission, setStudentSubmission] = useState<StudentSubmission | null>(null);
  const submissionId = searchParams.get('submissionId');

  if (!codeQuestion) {
    return <div>Loading...</div>;
  }

  // Modify the existing maxChars calculation
  const maxChars = codeQuestion.rules && codeQuestion.rules.find(rule => rule.startsWith('chars:'))
    ? parseInt(codeQuestion.rules.find(rule => rule.startsWith('chars:'))!.split(':')[1].trim())
    : DEFAULT_MAX_CHARS

  const maxFiles = codeQuestion.rules && codeQuestion.rules.find(rule => rule.startsWith('files:'))
    ? parseInt(codeQuestion.rules.find(rule => rule.startsWith('files:'))!.split(':')[1].trim())
    : DEFAULT_MAX_FILES


  const getFileCharLimit = (fileName: string, rules: string[]): number => {
    if (!codeQuestion.rules) return maxChars;
    const fileSpecificRule = codeQuestion.rules.find(rule => rule.startsWith(`chars-${fileName}:`))
    if (fileSpecificRule) {
      return parseInt(fileSpecificRule.split(':')[1].trim())
    }
    return maxChars
  }

  useEffect(() => {
    if (!codeQuestion || !courseId || !moduleId || !assessmentId) return;

    const savedState = localStorage.getItem(`assignmentState_${courseId}_${moduleId}_${assessmentId}_${codeQuestion.id}`);

    if (savedState) {
      const parsedState = JSON.parse(savedState);
      setCodeFiles(parsedState.codeFiles);
      setOutput(parsedState.output);
      setTestResults(parsedState.testResults);
      setActiveFileIndex(parsedState.activeFileIndex);
    } else {
      let initialFiles: CodeFile[] = []
      if (Array.isArray(codeQuestion.initialCode) && codeQuestion.initialCode.length > 0) {
        initialFiles = codeQuestion.initialCode.map((code, index) => {
          const fileName = (codeQuestion.codeName && codeQuestion.codeName[index]) || `File ${index + 1}`;
          const language = getLanguageFromExtension(fileName);
          const content = codeQuestion.rules.includes('inputs: y')
            ? insertInputs(code, codeQuestion.inputs, language, index)
            : code;
          return {
            name: fileName,
            language: language,
            content: content,
            history: { past: [], future: [] },
          };
        });
      } else if (typeof codeQuestion.initialCode === 'string') {
        const fileName = (codeQuestion.codeName && codeQuestion.codeName[0]) || 'File 1';
        const language = getLanguageFromExtension(fileName);
        const content = codeQuestion.rules.includes('inputs: y')
          ? insertInputs(codeQuestion.initialCode, codeQuestion.inputs, language, 0)
          : codeQuestion.initialCode;
        initialFiles = [{
          name: fileName,
          language: language,
          content: content,
          history: { past: [], future: [] },
        }];
      }

      setCodeFiles(initialFiles);

      // Initialize character counts
      const initialTotalChars = calculateTotalChars(initialFiles, codeQuestion.rules)
      setTotalChars(initialTotalChars)
      setCurrentFileChars(initialFiles[0]?.content.length || 0)
    }

    setSubmissionData({
      ...codeQuestion,
      submittedCode: [],
      output: '',
      testResults: []
    });

    // Check if the submit button should be hidden
    setIsSubmitHidden(codeQuestion.rules.includes('submit: n'));
  }, [codeQuestion, courseId, moduleId, assessmentId]); // Add IDs to dependency array

  useEffect(() => {
    const updateCharCounts = () => {
      const newTotalChars = calculateTotalChars(codeFiles, codeQuestion.rules);
      setTotalChars(newTotalChars);
      setCurrentFileChars(codeFiles[activeFileIndex]?.content.length || 0);
    };

    const intervalId = setInterval(updateCharCounts, 500);

    return () => clearInterval(intervalId);
  }, [codeFiles, activeFileIndex, codeQuestion.rules]);
  
  useEffect(() => {
    const fetchData = async () => {
      if (submissionId) {
        try {
          const response = await fetch(`../../../api/learn/teach/question/${codeQuestion.id}?courseId=${courseId}`);
          if (!response.ok) {
            throw new Error('Failed to fetch student submission');
          }
          const data: UserListQuestionv1_0_0 = await response.json();
          const submission = data.submissions[submissionId]?.submission as StudentSubmission;
          if (submission) {
            setStudentSubmission(submission);
            setCodeFiles(submission.submittedCode.map((content, index) => ({
              name: `file${index + 1}.js`,
              language: 'javascript',
              content,
              history: { past: [], future: [] },
            })));
          }
        } catch (error) {
          console.error('Error fetching student submission:', error);
          toast({
            title: 'Error',
            description: 'Failed to load student submission',
            variant: 'destructive',
          });
        }
      }
    };

    fetchData();
  }, [codeQuestion.id, submissionId, courseId]);

  const handleEditorChange = (value: string | undefined) => {
    if (value !== undefined) {
      updateFileHistory(activeFileIndex, value);

      const fileLimit = getFileCharLimit(codeFiles[activeFileIndex].name, codeQuestion.rules);
      const effectiveLimit = fileLimit === 0 ? maxChars : fileLimit;
      if (value.length > effectiveLimit) {
        toast({
          title: "Character limit exceeded",
          description: `You have exceeded the maximum character limit of ${effectiveLimit} for this file. Please reduce the content.`,
          variant: "destructive",
        });
        return;
      }

      const updatedCodeFiles = [...codeFiles];
      updatedCodeFiles[activeFileIndex].content = value;
      setCodeFiles(updatedCodeFiles);

      // Update total character count
      const newTotalChars = calculateTotalChars(updatedCodeFiles, codeQuestion.rules);
      setTotalChars(newTotalChars);
      setCurrentFileChars(value.length);

      setSubmissionData(prev => ({
        ...prev,
        submittedCode: updatedCodeFiles.map(file => file.content)
      }));

      // Trigger an immediate save
      const currentState = {
      codeQuestion,
      codeFiles: updatedCodeFiles,
      output,
      testResults,
      activeFileIndex
    };
    saveStateToLocalStorage(currentState, courseId, moduleId, assessmentId); // Pass IDs to save function
  }
  }

  const handleTabChange = (index: number) => {
    setActiveFileIndex(index);
  };

  const compareOutputs = (actualOutputs: string[], expectedOutputs: any[]): any[] => {
    // If expectedOutputs is not an array of arrays, wrap it in an array
    const normalizedExpectedOutputs = Array.isArray(expectedOutputs[0]) ? expectedOutputs : [expectedOutputs];

    return normalizedExpectedOutputs.flatMap((expectedFileOutputs, fileIndex) => {
      const actualFileOutput = actualOutputs[fileIndex] || '';
      const actualLines = actualFileOutput.trim().split('\n');

      // If expectedFileOutputs is empty, no outputs are expected for this file
      if (!expectedFileOutputs || expectedFileOutputs.length === 0) {
        return [{
          fileIndex,
          expectedOutput: 'No output expected',
          actualOutput: actualFileOutput,
          passed: actualFileOutput.trim() === ''
        }];
      }

      return expectedFileOutputs.map((expected: string, index: number) => ({
        fileIndex,
        expectedOutput: expected,
        actualOutput: actualLines[index] || '',
        passed: actualLines[index]?.trim() === expected.trim()
      }));
    });
  };

  const isWindowOpen = (window: Window | null): boolean => {
    return window !== null && !window.closed;
  };

  const runFile = () => {
    setIsRunAnimating(true);
    if (abortController) {
      abortController.abort();
    }
    const newAbortController = new AbortController();
    setAbortController(newAbortController);

    const currentFile = codeFiles[activeFileIndex];
    const fileExtension = `.${currentFile.name.split('.').pop()}`;
    const runType = settings.runSettings.fileExtensions[fileExtension] || 'Internal output';

    setOutput('');
    setTestResults([]);

    const openInNewTab = (content: string) => {
      if (isWindowOpen(openedWindow)) {
        openedWindow!.document.open();
        openedWindow!.document.write(content);
        openedWindow!.document.close();
      } else {
        const newWindow = window.open();
        if (newWindow) {
          newWindow.document.write(content);
          newWindow.document.close();
          setOpenedWindow(newWindow);
        }
      }
    };

    const handleInternalOutput = () => {
      if (currentFile.language === 'javascript') {
        const capturedOutput: string[] = [];
        const customConsole = {
          log: (...args: any[]) => {
            const output = args.join(' ');
            capturedOutput.push(output);
            if (!newAbortController.signal.aborted) {
              setOutput(prev => prev + output + '\n');
            }
          },
          error: (...args: any[]) => {
            const output = 'Error: ' + args.join(' ');
            capturedOutput.push(output);
            if (!newAbortController.signal.aborted) {
              setOutput(prev => prev + output + '\n');
            }
          }
        };

        try {
          const func = new Function('console', currentFile.content);
          func(customConsole);

          if (codeQuestion.rules.includes('outputs: y') && !newAbortController.signal.aborted) {
            const results = compareOutputs([capturedOutput.join('\n')], codeQuestion.outputs);
            setTestResults(results);
          }
        } catch (error) {
          if (!newAbortController.signal.aborted) {
            setOutput(`Error executing JavaScript: ${error}\n`);
          }
        }
      } else if (currentFile.language === 'html') {
        const processedHTML = processHTML(currentFile.content);
        if (!newAbortController.signal.aborted) {
          setOutput(`HTML content rendered in the output:\n\n${processedHTML}`);
        }
      } else {
        if (!newAbortController.signal.aborted) {
          setOutput(`Running ${currentFile.name}...\n\nContent:\n${currentFile.content}\n\nNote: Execution for ${currentFile.language} files is not implemented in this environment.`);
        }
      }
      if (!newAbortController.signal.aborted) {
        handleSetActiveTab('output'); // Updated
      }
    };

    if (runType === 'Open new guide' || runType === 'Both') {
      if (currentFile.language === 'html') {
        const processedHTML = processHTML(currentFile.content);
        openInNewTab(processedHTML);
      } else {
        openInNewTab(currentFile.content);
      }
    }

    if (runType === 'Internal output' || runType === 'Both') {
      handleInternalOutput();
    }
    setTimeout(() => setIsRunAnimating(false), 1000); // Stop animation after 1 second
  };

  const runThis = () => {
    setIsRunThisAnimating(true);
    if (abortController) {
      abortController.abort();
    }
    const newAbortController = new AbortController();
    setAbortController(newAbortController);

    const currentFile = codeFiles[activeFileIndex];
    const fileExtension = `.${currentFile.name.split('.').pop()}`;
    const runType = settings.runSettings.fileExtensions[fileExtension] || 'Internal output';

    setOutput('');
    setTestResults([]);

    const openInNewTab = (content: string) => {
      if (isWindowOpen(openedWindow)) {
        openedWindow!.document.open();
        openedWindow!.document.write(content);
        openedWindow!.document.close();
      } else {
        const newWindow = window.open();
        if (newWindow) {
          newWindow.document.write(content);
          newWindow.document.close();
          setOpenedWindow(newWindow);
        }
      }
    };

    const handleInternalOutput = () => {
      if (currentFile.language === 'javascript') {
        const capturedOutput: string[] = [];
        const customConsole = {
          log: (...args: any[]) => {
            const output = args.join(' ');
            capturedOutput.push(output);
            if (!newAbortController.signal.aborted) {
              setOutput(prev => prev + output + '\n');
            }
          },
          error: (...args: any[]) => {
            const output = 'Error: ' + args.join(' ');
            capturedOutput.push(output);
            if (!newAbortController.signal.aborted) {
              setOutput(prev => prev + output + '\n');
            }
          }
        };

        try {
          const func = new Function('console', currentFile.content);
          func(customConsole);
        } catch (error) {
          if (!newAbortController.signal.aborted) {
            setOutput(`Error executing JavaScript: ${error}\n`);
          }
        }
      } else if (currentFile.language === 'html') {
        if (!newAbortController.signal.aborted) {
          setOutput(`HTML content (not rendered):\n\n${currentFile.content}`);
        }
      } else {
        if (!newAbortController.signal.aborted) {
          setOutput(`Running ${currentFile.name}...\n\nContent:\n${currentFile.content}\n\nNote: Execution for ${currentFile.language} files is not implemented in this environment.`);
        }
      }
    };

    if (runType === 'Open new guide' || runType === 'Both') {
      openInNewTab(currentFile.content);
    }

    if (runType === 'Internal output' || runType === 'Both') {
      handleInternalOutput();
    }

    if (!newAbortController.signal.aborted) {
      handleSetActiveTab('output'); // Updated
    }
    setTimeout(() => setIsRunThisAnimating(false), 1000); // Stop animation after 1 second
  };

  const processHTML = (html: string) => {
    const parser = new DOMParser();
    const doc = parser.parseFromString(html, 'text/html');

    // Process CSS
    const styleLinks = doc.querySelectorAll('link[rel="stylesheet"]');
    styleLinks.forEach(link => {
      const href = link.getAttribute('href');
      if (href) {
        const cssFile = codeFiles.find(file => file.name === href);
        if (cssFile) {
          const style = doc.createElement('style');
          style.textContent = cssFile.content;
          link.parentNode?.replaceChild(style, link);
        }
      }
    });

    // Process JS
    const scripts = doc.querySelectorAll('script');
    scripts.forEach(script => {
      const src = script.getAttribute('src');
      if (src) {
        const jsFile = codeFiles.find(file => file.name === src);
        if (jsFile) {
          script.removeAttribute('src');
          script.textContent = jsFile.content;
        }
      }
    });

    return doc.documentElement.outerHTML;
  };


  const runApplication = (fileName: string) => {
    setIsRunAppAnimating(true);
    if (abortController) {
      abortController.abort();
    }
    const newAbortController = new AbortController();
    setAbortController(newAbortController);

    const indexFileName = settings.runSettings.applicationFileName || fileName;
    const indexFile = codeFiles.find(file => file.name === indexFileName);
    
    if (!indexFile) {
      toast({
        title: "Application file not found",
        description: `Could not find the file "${indexFileName}". Please check your settings.`,
        variant: "destructive",
      });
      setIsRunAppAnimating(false);
      return;
    }

    const indexExtension = `.${indexFile.name.split('.').pop()}`;
    const outputType = settings.runSettings.fileExtensions[indexExtension] || 'Internal output';

    setOutput('');
    setOutput(`Running application (${indexFile.name})...\n\n`);

    if (outputType === 'Open new guide' || outputType === 'Both') {
      if (isWindowOpen(openedWindow)) {
        openedWindow!.document.open();
        if (indexFile.name.endsWith('.html')) {
          const processedHTML = processHTML(indexFile.content);
          openedWindow!.document.write(processedHTML);
        } else {
          openedWindow!.document.write(indexFile.content);
        }
        openedWindow!.document.close();
      } else {
        const newWindow = window.open();
        if (newWindow) {
          if (indexFile.name.endsWith('.html')) {
            const processedHTML = processHTML(indexFile.content);
            newWindow.document.write(processedHTML);
          } else {
            newWindow.document.write(indexFile.content);
          }
          newWindow.document.close();
          setOpenedWindow(newWindow);
        }
      }
    }

    if (outputType === 'Internal output' || outputType === 'Both') {
      if (indexFile.name.endsWith('.html')) {
        const processedHTML = processHTML(indexFile.content);
        setOutput(prev => prev + `Processed HTML:\n\n${processedHTML}\n`);
      } else {
        const capturedOutput: string[] = [];
        const customConsole = {
          log: (...args: any[]) => {
            const output = args.join(' ');
            capturedOutput.push(output);
            if (!newAbortController.signal.aborted) {
              setOutput(prev => prev + output + '\n');
            }
          },
          error: (...args: any[]) => {
            const output = 'Error: ' + args.join(' ');
            capturedOutput.push(output);
            if (!newAbortController.signal.aborted) {
              setOutput(prev => prev + output + '\n');
            }
          }
        };

        try {
          const func = new Function('console', indexFile.content);
          func(customConsole);

          if (codeQuestion.rules.includes('outputs: y') && !newAbortController.signal.aborted) {
            const results = compareOutputs([capturedOutput.join('\n')], codeQuestion.outputs);
            setTestResults(results);
          }
        } catch (error) {
          if (!newAbortController.signal.aborted) {
            setOutput(`Error executing JavaScript: ${error}\n`);
          }
        }
      }
    }

    if (!newAbortController.signal.aborted) {
      handleSetActiveTab('output'); // Updated
    }
    setTimeout(() => setIsRunAppAnimating(false), 1000); // Stop animation after 1 second
  };

  const handleReturn = () => {
    const currentState = {
      codeFiles,
      output,
      testResults
    }
    saveStateToLocalStorage(currentState, courseId, moduleId, assessmentId); // Save state before returning
    router.back()
  };

  const checkSubmissionConflicts = (): { fileName: string; reason: string }[] => {
    const conflicts: { fileName: string; reason: string }[] = [];

    codeFiles.forEach(file => {
      const fileLimit = getFileCharLimit(file.name, codeQuestion.rules);
      if (file.content.length > fileLimit) {
        conflicts.push({
          fileName: file.name,
          reason: `Exceeds character limit of ${fileLimit}`
        });
      }
    });

    if (codeFiles.length > maxFiles) {
      conflicts.push({
        fileName: 'Project',
        reason: `Exceeds maximum number of files (${maxFiles})`
      });
    }

    // Check for failed tests only if they have been viewed
    if (testResultsViewed) {
      const failedTestsCount = testResults.filter(result => !result.passed).length;
      setFailedTests(failedTestsCount);
      setTotalTests(testResults.length);
    
      if (failedTestsCount > 0) {
        conflicts.push({
          fileName: 'Tests',
          reason: `${failedTestsCount} out of ${testResults.length} tests failed`
        });
      }
    }

    return conflicts;
  };

  const handleSubmit = () => {
    const conflicts = checkSubmissionConflicts();

    if (conflicts.length > 0) {
      setSubmissionConflicts(conflicts);
      setIsWarningDialogOpen(true);
    } else {
      setSubmissionData(prev => ({
        ...prev,
        testResults: testResults,
        output: output
      }));
      setIsConfirmDialogOpen(true);
    }
  }

  const handleConfirmSubmit = () => {
    setIsConfirmDialogOpen(false);
    onSubmit({
      ...submissionData,
      output: output
    } as QuestionSubmissionv1_0_0);
    
    // Navigate back to the assessment page
    router.push(`/learn/assessment/${assessmentId}?userId=${userId}&role=${role}&courseId=${courseId}&moduleId=${moduleId}`);
  }

  const handleCloseWarningDialog = () => {
    setIsWarningDialogOpen(false);
  };

  const handleNewFile = (name: string, language: string) => {
    if (codeFiles.length >= maxFiles) {
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
    const updatedCodeFiles = [...codeFiles, newFile];
    setCodeFiles(updatedCodeFiles);
    setActiveFileIndex(updatedCodeFiles.length - 1);
    setSubmissionData(prev => ({
      ...prev,
      submittedCode: updatedCodeFiles.map(file => file.content),
      codeName: [...(codeQuestion.codeName || []), name]
    }));
  };

  const onRenameFile = (oldName: string, newName: string) => {
    const updatedCodeFiles = codeFiles.map(file =>
      file.name === oldName ? { ...file, name: newName } : file
    );
    setCodeFiles(updatedCodeFiles);
    setActiveFileIndex(updatedCodeFiles.findIndex(file => file.name === newName));
    setSubmissionData(prev => ({
      ...prev,
      codeName: updatedCodeFiles.map(file => file.name)
    }))
  };

  const onDeleteFile = (name: string) => {
    const fileIndex = codeFiles.findIndex(file => file.name === name);
    if (fileIndex === -1) return;

    const updatedCodeFiles = codeFiles.filter(file => file.name !== name);

    setCodeFiles(updatedCodeFiles);

    if (activeFileIndex >= updatedCodeFiles.length) {
      setActiveFileIndex(updatedCodeFiles.length - 1);
    }

    setSubmissionData(prev => ({
      ...prev,
      submittedCode: updatedCodeFiles.map(file => file.content),
      codeName: updatedCodeFiles.map(file => file.name)
    }));
  };

  const onImportFile = async (file: File) => {
    if (codeFiles.length >= maxFiles) {
      toast({
        title: "File limit reached",
        description: `You have reached the maximum number of files (${maxFiles}). Please delete a file before importing a new one.`,
        variant: "destructive",
      });
      return;
    }
    try {
      const content = await file.text();
      const fileLimit = getFileCharLimit(file.name, codeQuestion.rules);
      const effectiveLimit = fileLimit === 0 ? maxChars : fileLimit;
      if (content.length > effectiveLimit) {
        toast({
          title: "Character limit exceeded",
          description: `Importing this file would exceed the maximum character limit of ${effectiveLimit} for this file. Please reduce the content before importing.`,
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
      const updatedCodeFiles = [...codeFiles, newFile];
      setCodeFiles(updatedCodeFiles);
      setActiveFileIndex(updatedCodeFiles.length - 1);
      setSubmissionData(prev => ({
        ...prev,
        submittedCode: updatedCodeFiles.map(file => file.content),
        codeName: [...(codeQuestion.codeName || []), file.name]
      }));
    } catch (error) {
      console.error('Error reading file:', error);
      toast({
        title: "Error importing file",
        description: "An error occurred while importing the file. Please try again.",
        variant: "destructive",
      });
    }
  };

  const handleExportFiles = async (exportAll: boolean, singleFileName?: string) => {
    const zip = new JSZip();
    const folder = zip.folder(codeQuestion.title);

    if (folder) {
      if (exportAll) {
        codeFiles.forEach(file => {
          folder.file(file.name, file.content);
        });
      } else if (singleFileName) {
        const fileToExport = codeFiles.find(file => file.name === singleFileName);
        if (fileToExport) {
          folder.file(fileToExport.name, fileToExport.content);
        }
      }

      const content = await zip.generateAsync({ type: "blob" });
      const url = window.URL.createObjectURL(content);
      const a = document.createElement('a');
      a.href = url;
      a.download = exportAll
        ? `${codeQuestion.title}.zip`
        : `${codeQuestion.title}_${singleFileName}.zip`;
      a.click();
      window.URL.revokeObjectURL(url);
    }
  };

  const renderBottomPanel = () => {
    if (!activeTab) return null
    const hideTestOutputs = codeQuestion.rules.includes('tests-outputs: n');
    return (
      <div className={`h-full overflow-auto p-4 ${
        mode === 'light' ? 'bg-white text-gray-800' :
        mode === 'dark' ? 'bg-gray-800 text-gray-200' :
        'bg-black text-white'
      }`}>
        {activeTab === 'problems' && <ProblemsTab codeQuestion={codeQuestion} mode={mode} />}
        {activeTab === 'output' && <OutputTab output={output} mode={mode} />}
        {activeTab === 'testResults' && <TestResultsTab testResults={testResults} mode={mode} hideTestOutputs={hideTestOutputs} />}
      </div>
    )
  }

  const handleReset = () => {
    if (codeQuestion && courseId && moduleId && assessmentId) { // Check if IDs are available
      localStorage.removeItem(`assignmentState_${courseId}_${moduleId}_${assessmentId}_${codeQuestion.id}`);
    }
    window.location.reload();
  };

  const handlePanelLayout = useCallback((sizes: number[]) => {
    const lessonPanelSize = sizes[0];
    const totalWidth = window.innerWidth;
    const lessonPanelWidth = (lessonPanelSize / 100) * totalWidth;
    const minCodeEditorWidth = 300; // Minimum width for the code editor panel

    if (lessonPanelWidth > totalWidth - minCodeEditorWidth) {
      const newLessonPanelSize = ((totalWidth - minCodeEditorWidth) /totalWidth) * 100;
      lessonPanelRef.current?.resize(newLessonPanelSize);
    } else if (lessonPanelWidth < 300 && !isLessonCollapsed) {
      setIsLessonCollapsed(true);
      setPrevLessonPanelSize(lessonPanelSize);
      lessonPanelRef.current?.collapse();
    } else if (lessonPanelWidth >= 300 && isLessonCollapsed) {
      setIsLessonCollapsed(false);
    }
  }, [isLessonCollapsed]);

  const handleResizeHandleDoubleClick = useCallback(() => {
    if (isLessonCollapsed) {
      const totalWidth = window.innerWidth;
      const newSize = (300 / totalWidth) * 100;
      lessonPanelRef.current?.resize(newSize);
      setIsLessonCollapsed(false);
    } else {
      setPrevLessonPanelSize(lessonPanelRef.current?.getSize() || 30);
      lessonPanelRef.current?.collapse();
      setIsLessonCollapsed(true);
    }
  }, [isLessonCollapsed]);

  const handleResizeHandleClick = useCallback(() => {
    if (isLessonCollapsed) {
      const totalWidth = window.innerWidth;
      const newSize = (300 / totalWidth) * 100;
      lessonPanelRef.current?.resize(newSize);
      setIsLessonCollapsed(false);
    }
  }, [isLessonCollapsed]);

  const updateFileHistory = (fileIndex: number, newContent: string) => {
    const updatedFiles = [...codeFiles];
    const currentFile = updatedFiles[fileIndex];

    // Only update history if the content has changed
    if (currentFile.content !== newContent) {
      currentFile.history.past.push(currentFile.content);
      currentFile.history.future = [];
      currentFile.content = newContent;
      setCodeFiles(updatedFiles);
    }
  };

  const undo = useCallback(() => {
    const updatedFiles = [...codeFiles];
    const currentFile = updatedFiles[activeFileIndex];

    if (currentFile.history.past.length >0) {
      const previousContent = currentFile.history.past.pop()!;
      currentFile.history.future.push(currentFile.content);
      currentFile.content = previousContent;
      setCodeFiles(updatedFiles);

      // Update Monaco Editor content
      if (editorRef.current) {
        editorRef.current.setValue(previousContent);
      }
    }
  }, [codeFiles, activeFileIndex]);

  const redo = useCallback(() => {
    const updatedFiles = [...codeFiles];
    const currentFile = updatedFiles[activeFileIndex];

    if (currentFile.history.future.length > 0) {
      const nextContent = currentFile.history.future.pop()!;
      currentFile.history.past.push(currentFile.content);
      currentFile.content = nextContent;
      setCodeFiles(updatedFiles);

      // Update Monaco Editor content
      if (editorRef.current) {
        editorRef.current.setValue(nextContent);
      }
    }
  }, [codeFiles, activeFileIndex]);

  const startContinuousUndo = useCallback(() => {
    setIsUndoing(true);
    const performUndo = () => {
      undo();
      undoTimerRef.current = setTimeout(performUndo, 25);
    };
    undoTimerRef.current = setTimeout(performUndo, 1000);
  }, [undo]);

  const startContinuousRedo = useCallback(() => {
    setIsRedoing(true);
    const performRedo = () => {
      redo();
      redoTimerRef.current = setTimeout(performRedo, 25);
    };
    redoTimerRef.current = setTimeout(performRedo, 1000);
  }, [redo]);

  const stopContinuousUndo = useCallback(() => {
    setIsUndoing(false);
    if (undoTimerRef.current) {
      clearTimeout(undoTimerRef.current);
    }
  }, []);

  const stopContinuousRedo = useCallback(() => {
    setIsRedoing(false);
    if (redoTimerRef.current) {
      clearTimeout(redoTimerRef.current);
    }
  }, []);

  const handleOpenSettings = () => {
    setIsSettingsOpen(true);
  };

  const handleCloseSettings = () => {
    setIsSettingsOpen(false);
  };

  const handleConfirmSettings = (newSettings: Settings) => {
    setSettings(newSettings);
    setIsSettingsOpen(false);
  };

  const executeJavaScript = (code: string) => {
    try {
      // Criar um contexto isolado para execução
      const context = {
        console: {
          log: (...args: any[]) => setOutput(prev => prev + args.join(' ') + '\n'),
          error: (...args: any[]) => setOutput(prev => prev + 'Error: ' + args.join(' ') + '\n'),
        }
      };
      const func = new Function('context', `with(context){${code}}`);
      func(context);
    } catch (error) {
      setOutput(`Error executing JavaScript: ${error}\n`);
    }
  };

  useEffect(() => {
    return () => {
      if (abortController) {
        abortController.abort();
      }
    };
  }, [abortController]);

  useEffect(() => {
    if (!codeQuestion) return;

    const autoSaveTimer = setInterval(() => {
      const currentState = {
        codeQuestion,
        codeFiles,
        output,
        testResults,
        activeFileIndex
      };
      saveStateToLocalStorage(currentState);
    }, 3000); // Auto-save every 3 seconds

    return () =>clearInterval(autoSaveTimer);
  }, [codeQuestion, codeFiles, output, testResults, activeFileIndex]);

  const handleToggleSplitView = () => {
    if (isSplitViewActive) {
      setIsSplitViewActive(false);
      setSplitViewFileIndex(null); // Updated
    } else {
      setIsFileSelectorOpen(true);
    }
  };

  const handleSelectSplitViewFile = (fileName: string) => {
    const fileIndex = codeFiles.findIndex(file => file.name === fileName);
    if (fileIndex !== -1) {
      setSplitViewFileIndex(fileIndex);
      setIsSplitViewActive(true);
    }
    setIsFileSelectorOpen(false);
  };

  const handleSplitViewEditorChange = (value: string | undefined) => {
    if (value !== undefined && splitViewFileIndex !== null) {
      const updatedCodeFiles = [...codeFiles];
      updatedCodeFiles[splitViewFileIndex].content = value;
      setCodeFiles(updatedCodeFiles);
    }
  };

  const handleSetActiveTab = (tab: 'problems' | 'output' | 'testResults' | null) => {
    setActiveTab(tab);
    if (tab === 'testResults') {
      setTestResultsViewed(true);
    }
  };

  return (
    <div className={`h-screen ${
      mode === 'light' ? 'bg-gray-100 text-gray-800' :
      mode === 'dark' ? 'bg-gray-900 text-gray-200' :
      'bg-black text-white'
    }`}>
      <PanelGroup direction="horizontal" onLayout={handlePanelLayout}>
        <Panel
          ref={lessonPanelRef}
          defaultSize={30}
          minSize={0}
          maxSize={70}
          collapsible={true}
          onCollapse={() => setIsLessonCollapsed(true)}
          onExpand={() => setIsLessonCollapsed(false)}
        >
          <LessonPanel
            codeQuestion={codeQuestion}
            onReturn={handleReturn}
            onSubmit={handleSubmit}
            onReset={handleReset}
            mode={mode}
            isSubmitDisabled={isSubmitDisabled}
            isSubmitHidden={isSubmitHidden}
            hierarchy={hierarchy}
          />
        </Panel>
        <PanelResizeHandle className={`transition-colors ${
          mode === 'light'
            ? 'bg-gray-300 hover:bg-gray-400'
            : mode === 'dark'
            ? 'bg-gray-700 hover:bg-gray-600'
            : 'bg-yellow-500 hover:bg-yellow-400'
        } ${isLessonCollapsed ? 'w-4 cursor-pointer' : 'w-2'}`}
          onDoubleClick={handleResizeHandleDoubleClick}
          onClick={handleResizeHandleClick}
        />
        <Panel minSize={30}>
          <PanelGroup direction="vertical">
            <Panel minSize={50}>
              <div className="flex flex-col h-full">
                <TopMenu
                  mode={mode}
                  setMode={setMode}
                  onRunFile={runFile}
                  onRunApplication={runApplication}
                  onNewFile={handleNewFile}
                  onRenameFile={onRenameFile}
                  onDeleteFile={onDeleteFile}
                  onImportFile={onImportFile}
                  onExportFiles={handleExportFiles}
                  activeFileName={codeFiles[activeFileIndex]?.name || ''}
                  currentFileCount={codeFiles.length}
                  maxFiles={maxFiles}
                  onUndo={undo}
                  onRedo={redo}
                  onUndoStart={startContinuousUndo}
                  onUndoStop={stopContinuousUndo}
                  onRedoStart={startContinuousRedo}
                  onRedoStop={stopContinuousRedo}
                  isUndoing={isUndoing}
                  isRedoing={isRedoing}
                  canUndo={codeFiles[activeFileIndex]?.history.past.length > 0}
                  canRedo={codeFiles[activeFileIndex]?.history.future.length > 0}
                  onOpenSettings={handleOpenSettings}
                  onRunThis={runThis}
                  onToggleSplitView={handleToggleSplitView}
                  isSplitViewActive={isSplitViewActive}
                  isRunAnimating={isRunAnimating}
                  isRunThisAnimating={isRunThisAnimating}
                  isRunAppAnimating={isRunAppAnimating}
                  activeFile={codeFiles[activeFileIndex]}
                  onReset={handleReset} // Added onReset prop
                />
                <div className="flex-grow flex flex-col overflow-hidden">
                  <CodeTabs files={codeFiles} activeIndex={activeFileIndex} onTabChange={handleTabChange} mode={mode} />
                  <div className="flex-grow overflow-hidden">
                    {codeFiles.length > 0 ? (
                      <div className={`flex h-full ${isSplitViewActive ? 'space-x-2' : ''}`}>
                        <div className={`${isSplitViewActive ? 'w-1/2' : 'w-full'}`}>
                          <MonacoEditor
                            height="100%"
                            language={codeFiles[activeFileIndex]?.language || 'plaintext'}
                            value={codeFiles[activeFileIndex]?.content || ''}
                            theme={mode === 'light' ? 'vs-light' : mode === 'dark' ? 'vs-dark' : 'hc-black'}
                            onChange={handleEditorChange}
                            onMount={(editor, monaco) => {
                              editorRef.current = editor;
                              // Disable undo/redo keyboard shortcuts
                              editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyZ, () => null);
                              editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyY, () => null);
                              editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyMod.Shift | monaco.KeyCode.KeyZ, () => null);

                              // Add HTML-specific features
                              if (codeFiles[activeFileIndex]?.language === 'html') {
                                monaco.languages.html.htmlDefaults.setOptions({
                                  format: {
                                    tabSize: 2,
                                    insertSpaces: true,
                                  },
                                  suggest: {
                                    html5: true,
                                  },
                                });
                              }
                            }}
                            options={{
                              minimap: { enabled: false },
                              fontSize: 14,
                              scrollBeyondLastLine: false,
                              automaticLayout: true,
                              undoStop: false,
                              wordWrap: 'on',
                              autoClosingBrackets: 'always',
                              autoClosingQuotes: 'always',
                              formatOnPaste: true,
                              formatOnType: true,
                            }}
                          />
                        </div>
                        {isSplitViewActive && splitViewFileIndex !== null && (
                          <div className="w-1/2">
                            <MonacoEditor
                              height="100%"
                              language={codeFiles[splitViewFileIndex].language}
                              value={codeFiles[splitViewFileIndex].content}
                              onChange={handleSplitViewEditorChange}
                              theme={mode === 'light' ? 'vs-light' : mode === 'dark' ? 'vs-dark' : 'hc-black'}
                              options={{
                                minimap: { enabled: false },
                                fontSize: 14,
                                scrollBeyondLastLine: false,
                                automaticLayout: true,
                                wordWrap: 'on',
                              }}
                            />
                          </div>
                        )}
                      </div>
                    ) : (
                      <div className="flex-grow overflow-auto flex items-center justify-center p-4">
                        <Alert variant={mode === 'high-contrast' ? 'destructive' : 'default'} className={`w-full max-w-md ${
                          mode === 'light' ? 'bg-white border-gray-200 text-gray-800' :
                          mode === 'dark' ? 'bg-gray-800 border-gray-700 text-gray-200' :
                          'bg-black border-yellow-300 text-yellow-300'
                        }`}>
                          <AlertCircle className={`h-4 w-4 ${
                            mode === 'high-contrast' ? 'text-yellow-300' : ''
                          }`} />
                          <AlertTitle className="mb-2">No code files</AlertTitle>
                          <AlertDescription>
                            There are no code files available. Create a new file to start coding.
                          </AlertDescription>
                        </Alert>
                      </div>
                    )}
                  </div>
                  <CharacterCount
                    currentFileChars={currentFileChars}
                    maxFileChars={getFileCharLimit(codeFiles[activeFileIndex]?.name, codeQuestion.rules) || maxChars}
                    totalChars={totalChars}
                    maxTotalChars={maxChars}
                    currentFileCount={codeFiles.length}
                    maxFiles={maxFiles}
                    mode={mode}
                  />
                </div>
              </div>
            </Panel>
            <PanelResizeHandle className={`h-2 transition-colors ${
              mode === 'light' ? 'bg-gray-300 hover:bg-gray-400' :
              mode === 'dark' ? 'bg-gray-700 hover:bg-gray-600' :
              'bg-yellow-500 hover:bg-yellow-400'
            }`} />
            <Panel defaultSize={25} minSize={0}> {/* UPDATED LINE */}
              <div className="flex flex-col h-full">
                <BottomMenu activeTab={activeTab} setActiveTab={handleSetActiveTab} mode={mode} hideTestOutputs={codeQuestion.rules.includes('tests-outputs: n')}/>
                <div className="flex-grow overflow-auto">
                  {renderBottomPanel()}
                </div>
              </div>
            </Panel>
          </PanelGroup>
        </Panel>
      </PanelGroup>
      <SubmissionWarningDialog
        isOpen={isWarningDialogOpen}
        onClose={handleCloseWarningDialog}
        onSubmit={handleConfirmSubmit}
        conflicts={submissionConflicts}
        failedTests={failedTests}
        totalTests={totalTests}
        mode={mode}
      />
      <SettingsModal
        isOpen={isSettingsOpen}
        onClose={handleCloseSettings}
        onConfirm={handleConfirmSettings}
        mode={mode}
        initialSettings={settings}
      />
      <ConfirmSubmissionDialog
        isOpen={isConfirmDialogOpen}
        onClose={() => setIsConfirmDialogOpen(false)}
        onConfirm={handleConfirmSubmit}
        mode={mode}
      />
      <SplitViewFileSelector
        isOpen={isFileSelectorOpen}
        onClose={() => setIsFileSelectorOpen(false)}
        onSelectFile={handleSelectSplitViewFile}
        files={codeFiles}
        mode={mode}
      />
    </div>
  )
}

const ProblemsTab = ({ codeQuestion, mode }: { codeQuestion: CodeQuestionv1_0_0, mode: string }) => {
  const hideTestOutputs = codeQuestion.rules.includes('tests-outputs: n');

  return (
    <div>
      <h2 className={`text-xl font-bold mb-2 ${
        mode === 'light' ? 'text-gray-800' :
        mode === 'dark' ? 'text-gray-200' :
        'text-white'
      }`}>Problem Details</h2>
      {codeQuestion.rules.includes('inputs: y') && (
        <div className="mb-4">
          <h3 className={`text-lg font-semibold ${
            mode === 'light' ? 'text-gray-800' :
            mode === 'dark' ? 'text-gray-200' :
            'text-white'
          }`}>Inputs:</h3>
          <ul className="list-disc list-inside">
            {codeQuestion.inputs.map((input, index) => (
              <li key={index} className={
                mode === 'light' ? 'text-gray-800' :
                mode === 'dark' ? 'text-gray-200' :
                'text-white'
              }>{JSON.stringify(input)}</li>
            ))}
          </ul>
        </div>
      )}
      {codeQuestion.rules.includes('outputs: y') && !hideTestOutputs && (
        <div>
          <h3 className={`text-lg font-semibold ${
            mode === 'light' ? 'text-gray-800' :
            mode === 'dark' ?'text-gray-200' :
            'text-white'
          }`}>Expected Outputs:</h3>
          <ul className="list-disc list-inside">
            {codeQuestion.outputs.map((output, index) => (
              <li key={index} className={
                mode === 'light' ? 'text-gray-800' :
                mode === 'dark' ? 'text-gray-200' :
                'text-white'
              }>{output}</li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}

const OutputTab = ({ output, mode }: { output: string, mode: string }) => {
  return (
    <div>
      <h2 className={`text-xl font-bold mb-2 ${
        mode === 'light' ? 'text-gray-800' :
                mode ==='dark' ? 'text-gray-200' :
        'text-white'
      }`}>Output</h2>
      <pre className={`p-2 rounded ${
        mode === 'light' ? 'bg-gray-100 text-gray-800' :
        mode === 'dark' ? 'bg-gray-800 text-gray-200' :
        'bg-black text-white'
      }`}>
        {output || 'No output yet. Run your code to see the results.'}
      </pre>
    </div>
  )
}

interface TestResultsTabProps {
  testResults: any[];
  mode: string;
  hideTestOutputs: boolean;
}
/*
const TestResultsTab2 = ({ testResults, mode, hideTestOutputs }: TestResultsTabProps) => {
  const getColorClass = (passed: boolean) => {
    if (mode === 'light') {
      return passed ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'
    } else if (mode === 'dark') {
      return passed ? 'bg-green-800 text-green-100' : 'bg-red-800 text-red-100'
    } else {
      return passed ? 'bg-yellow-300 text-black' : 'bg-yellow-800 text-yellow-100'
    }
  }

  const groupedResults = testResults.reduce((acc, result) => {
    if (!acc[result.fileIndex]) {
      acc[result.fileIndex] = [];
    }
    acc[result.fileIndex].push(result);
    return acc;
  }, {} as Record<number, typeof testResults>);

  return (
    <div>
      <h2 className={`text-xl font-bold mb-4 ${
        mode === 'light' ? 'text-gray-800' :
        mode === 'dark' ? 'text-gray-200' :
        'text-yellow-300'
      }`}>Test Results</h2>
      {Object.entries(groupedResults).length > 0 ? (
        <>
          {Object.entries(groupedResults).map(([fileIndex, fileResults]) => (
            <div key={fileIndex} className="mb-6">
              <h3 className={`text-lg font-semibold mb-2 ${
                mode === 'light' ? 'text-gray-700' :
                mode === 'dark' ? 'text-gray-300' :
                'text-yellow-200'
              }`}>File {parseInt(fileIndex) + 1}</h3>
              {fileResults.map((result, index) => (
                <div key={index} className={`mb-4 p-4 rounded ${getColorClass(result.passed)}`}>
                  <div className="flex items-center mb-2">
                    <span className={`font-bold mr-2 ${
                      mode === 'light' ? 'text-gray-800' :
                      mode === 'dark' ? 'text-gray-200' :
                      'text-yellow-300'
                    }`}>Test #{index + 1}:</span>
                    <p className="font-bold">Status: {result.passed ? 'Passed' : 'Failed'}</p>
                  </div>
                  {!hideTestOutputs && (
                    <div className="mb-2">
                      <p className="font-semibold">Expected Output:</p>
                      <pre className="ml-4 whitespace-pre-wrap">{result.expectedOutput}</pre>
                    </div>
                  )}
                  <div className="mb-2">
                    <p className="font-semibold">Actual Output:</p>
                    <pre className="ml-4 whitespace-pre-wrap">{result.actualOutput}</pre>
                  </div>
                </div>
              ))}
            </div>
          ))}
          <p className={`mt-4 font-bold ${
            mode === 'light' ? 'text-gray-800' :
            mode === 'dark' ? 'text-gray-200' :
            'text-yellow-300'
          }`}>
            Passed: {testResults.filter(r => r.passed).length} / {testResults.length}
          </p>
        </>
      ) : (
        <p className={
          mode === 'light' ? 'text-gray-600' :
          mode === 'dark' ? 'text-gray-400' :
          'text-yellow-200'
        }>No test results available. Run your code to see the results.</p>
      )}
    </div>
  )
}

*/