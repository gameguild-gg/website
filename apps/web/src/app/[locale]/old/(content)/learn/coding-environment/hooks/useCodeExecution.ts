import { useState } from 'react';
import { CodeFile } from '../types/codeEditor';
import { Settings } from '../components/SettingsModal';

const languageMap: { [key: string]: string } = {
  js: 'javascript',
  jsx: 'javascript',
  ts: 'typescript',
  tsx: 'typescript',
  py: 'python',
  rb: 'ruby',
  lua: 'lua',
  html: 'html',
};

export function useCodeExecution(files: CodeFile[], settings: Settings) {
  const [output, setOutput] = useState('');

  const executeJavaScript = (code: string) => {
    try {
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

  const runFile = (file: CodeFile) => {
    const fileExtension = `.${file.name.split('.').pop()}`;
    const runType = settings.runSettings.fileExtensions[fileExtension] || 'Internal output';
    const language = languageMap[file.language] || file.language;

    setOutput('');

    if (runType === 'Open new guide' || runType === 'Both') {
      const newWindow = window.open();
      if (newWindow) {
        newWindow.document.write(file.content);
        newWindow.document.close();
      }
    }

    if (runType === 'Internal output' || runType === 'Both') {
      // Local where we obtain the string of the identified language code
      switch (language) {
        case 'javascript':
          executeJavaScript(file.content);
          break;
        case 'html':
          setOutput(`HTML content would be rendered in a browser:\n\n${file.content}`);
          break;
        case 'typescript':
        case 'python':
        case 'ruby':
        case 'lua':
          // Future implementation: Transpile to JavaScript
          setOutput(`Execution for ${language} files is not yet implemented. Future steps:\n1. Transpile ${language} to JavaScript\n2. Execute the resulting JavaScript code`);
          break;
        default:
          setOutput(`Running ${file.name}...\n\nContent:\n${file.content}\n\nNote: Execution for ${language} files is not implemented in this environment.`);
      }
    }
  };

  const runApplication = (activeFileName: string) => {
    const indexFileName = settings.runSettings.applicationFileName || activeFileName;
    const indexFile = files.find(file => file.name === indexFileName);
    
    if (!indexFile) {
      setOutput(`Application file "${indexFileName}" not found.`);
      return;
    }

    setOutput(`Running application (${indexFile.name})...\n\n`);

    const processHTML = (html: string) => {
      const parser = new DOMParser();
      const doc = parser.parseFromString(html, 'text/html');

      // Process CSS
      const styleLinks = doc.querySelectorAll('link[rel="stylesheet"]');
      styleLinks.forEach(link => {
        const href = link.getAttribute('href');
        if (href) {
          const cssFile = files.find(file => file.name === href);
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
          const jsFile = files.find(file => file.name === src);
          if (jsFile) {
            script.removeAttribute('src');
            script.textContent = jsFile.content;
          }
        }
      });

      return doc.documentElement.outerHTML;
    };

    if (indexFile.name.endsWith('.html')) {
      const processedHTML = processHTML(indexFile.content);
      setOutput(prev => prev + `Processed HTML:\n${processedHTML}\n`);

      const newWindow = window.open();
      if (newWindow) {
        newWindow.document.write(processedHTML);
        newWindow.document.close();
      }
    } else {
      executeJavaScript(indexFile.content);
    }
  };

  return { output, runFile, runApplication };
}

