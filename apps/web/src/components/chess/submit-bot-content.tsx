'use client';

import { useCallback, useEffect, useState } from 'react';
import { useDropzone } from 'react-dropzone';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/chess/ui/card';
import { Button } from '@/components/chess/ui/button';
import { Alert, AlertDescription, AlertTitle } from '@/components/chess/ui/alert';
import { AlertTriangle, CheckCircle, FileArchive, FileCode, Info, Loader2, Upload, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import JSZip from 'jszip';
import { getSession } from 'next-auth/react';
import { GetSessionReturnType } from '@/config/auth.config';
import { CompetitionsApi } from '@game-guild/apiclient';

// Define allowed file extensions
const ALLOWED_EXTENSIONS = ['.c', '.cpp', '.cxx', '.h', '.hpp', '.hxx', '.zip'];

// Function to check if a file has an allowed extension
function hasAllowedExtension(filename: string): boolean {
  const extension = '.' + filename.split('.').pop()?.toLowerCase();
  return ALLOWED_EXTENSIONS.includes(extension);
}

export default function SubmitBotContent() {
  const [files, setFiles] = useState<File[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isValidatingZip, setIsValidatingZip] = useState(false);
  const [submitStatus, setSubmitStatus] = useState<{
    type: 'success' | 'error' | 'info' | null;
    message: string | null;
  }>({ type: null, message: null });
  const [accessToken, setAccessToken] = useState('');
  const api = new CompetitionsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  async function getAccessToken() {
    if (accessToken) return;
    const localSession = (await getSession()) as unknown as GetSessionReturnType;
    if (localSession) {
      const token = localSession.user.accessToken;
      setAccessToken(token);
    } else {
      console.error('No session found');
    }
  }

  useEffect(() => {
    getAccessToken();
  }, []);

  // Check if current files include a zip file
  const hasZipFile = files.some((file) => file.name.toLowerCase().endsWith('.zip'));

  // Check if current files include C++ files
  const hasCppFiles = files.some((file) => !file.name.toLowerCase().endsWith('.zip'));

  const validateZipFile = async (file: File): Promise<{ valid: boolean; message?: string }> => {
    setIsValidatingZip(true);
    setSubmitStatus({
      type: 'info',
      message: 'Validating zip file contents... This may take a moment.',
    });

    try {
      const zipContent = await file.arrayBuffer();
      const zip = new JSZip();
      const zipContents = await zip.loadAsync(zipContent);

      const invalidFiles: string[] = [];

      await Promise.all(
        Object.keys(zipContents.files).map(async (filename) => {
          const zipFile = zipContents.files[filename];

          // Skip directories
          if (zipFile.dir) return;

          // Check if the file has an allowed extension
          if (!hasAllowedExtension(filename)) {
            invalidFiles.push(filename);
          }
        }),
      );

      if (invalidFiles.length > 0) {
        return {
          valid: false,
          message: `Your zip file contains invalid file types. Only C++ related files (.c, .cpp, .cxx, .h, .hpp, .hxx) are allowed. Bake sure you are zipping the files properly. Offending files: ${JSON.stringify(invalidFiles)}`,
        };
      }

      return { valid: true };
    } catch (error) {
      console.error('Error validating zip:', error);
      return {
        valid: false,
        message: 'Failed to process zip file. The file may be corrupted, invalid or password protected. Please try again.',
      };
    } finally {
      setIsValidatingZip(false);
      setSubmitStatus({ type: null, message: null });
    }
  };

  const onDrop = useCallback(
    async (acceptedFiles: File[]) => {
      // Reset status when new files are added
      setSubmitStatus({ type: null, message: null });

      // Check if we already have a zip file and trying to add more files
      if (hasZipFile) {
        setSubmitStatus({
          type: 'error',
          message: 'You already have a zip file. Please remove it first if you want to upload different files.',
        });
        return;
      }

      // Check if we're trying to add a zip file when we already have C++ files
      const newZipFiles = acceptedFiles.filter((file) => file.name.toLowerCase().endsWith('.zip'));
      if (hasCppFiles && newZipFiles.length > 0) {
        setSubmitStatus({
          type: 'error',
          message: 'You already have C++ files. Please remove them first if you want to upload a zip file instead.',
        });
        return;
      }

      // Filter for only allowed file extensions
      const validFiles: File[] = [];
      const invalidFiles: File[] = [];

      for (const file of acceptedFiles) {
        if (hasAllowedExtension(file.name)) {
          // If it's a zip file, validate its contents
          if (file.name.toLowerCase().endsWith('.zip')) {
            const zipValidation = await validateZipFile(file);
            if (zipValidation.valid) {
              validFiles.push(file);
            } else {
              setSubmitStatus({
                type: 'error',
                message: zipValidation.message || 'Invalid zip file contents.',
              });
              return;
            }
          } else {
            validFiles.push(file);
          }
        } else {
          invalidFiles.push(file);
        }
      }

      if (invalidFiles.length > 0) {
        setSubmitStatus({
          type: 'error',
          message: `Invalid file type(s): ${invalidFiles.map((f) => f.name).join(', ')}. Only C++ related files (.c, .cpp, .cxx, .h, .hpp, .hxx) and .zip files are allowed.`,
        });
        return;
      }

      // Check if there's more than one zip file
      const zipFiles = validFiles.filter((file) => file.name.toLowerCase().endsWith('.zip'));
      if (zipFiles.length > 1) {
        setSubmitStatus({
          type: 'error',
          message: 'Only one zip file is allowed per submission.',
        });
        return;
      }

      // If there's a zip file, don't allow other files
      if (zipFiles.length === 1 && validFiles.length > 1) {
        setSubmitStatus({
          type: 'error',
          message: 'When submitting a zip file, no other files should be included.',
        });
        return;
      }

      setFiles((prevFiles) => [...prevFiles, ...validFiles]);
    },
    [hasZipFile, hasCppFiles],
  );

  const { getRootProps, getInputProps, isDragActive, isDragReject } = useDropzone({
    onDrop,
    accept: {
      'text/x-c': ['.c'],
      'text/x-c++src': ['.cpp', '.cxx'],
      'text/x-h': ['.h', '.hpp', '.hxx'],
      'application/zip': ['.zip'],
    },
    multiple: true,
    disabled: isSubmitting || isValidatingZip,
  });

  const removeFile = (index: number) => {
    setFiles((prevFiles) => prevFiles.filter((_, i) => i !== index));
  };

  const handleSubmit = async () => {
    if (files.length === 0) {
      setSubmitStatus({
        type: 'error',
        message: 'Please select at least one file to upload.',
      });
      return;
    }

    setIsSubmitting(true);
    setSubmitStatus({ type: null, message: null });

    let zipdata: ArrayBuffer;
    let zipFilename = 'submission.zip';
    // if there is only one file and it is a zip get the arraybuffer
    if (files.length === 1 && files[0].name.toLowerCase().endsWith('.zip')) {
      zipdata = await files[0].arrayBuffer();
      zipFilename = files[0].name;
    }
    // else if there is a list of c++ files
    else if (files.length > 1) {
      const zip = new JSZip();
      files.forEach((file) => {
        zip.file(file.name, file);
      });
      zipdata = await zip.generateAsync({ type: 'arraybuffer' });
    }

    // check if zipData size is less than 10MB
    if (zipdata && zipdata.byteLength > 10 * 1024 * 1024) {
      setSubmitStatus({
        type: 'error',
        message: 'Zip file size must be less than 10MB.',
      });
      setIsSubmitting(false);
      return;
    }

    try {
      // Create a File object from the zip data
      const zipFile = new File([zipdata as ArrayBuffer], zipFilename, {
        type: 'application/zip',
      });

      const response = await api.competitionControllerSubmitChessAgent(
        {
          file: {
            value: zipFile,
          },
        },
        {
          headers: {
            Authorization: `Bearer ${accessToken}`,
          },
        },
      );

      if (response.status === 200 || response.status === 201) {
        console.log(JSON.stringify(response));
        setSubmitStatus({
          type: 'success',
          message: 'Bot submitted successfully!',
        });
        setFiles([]);
      } else {
        let errorMessage = '';
        if (response && response.body && response.body['raw'] && response.body['raw']['stderr']) {
          errorMessage = response.body['raw']['stderr'];
          // replace \n with <br />
          errorMessage = errorMessage.replace(/\n/g, '<br />');
        } else errorMessage = JSON.stringify(response);

        console.error(errorMessage);
        setSubmitStatus({
          type: 'error',
          message: errorMessage,
        });
      }
    } catch (error) {
      console.error(error);
      setSubmitStatus({
        type: 'error',
        message: 'Failed to submit bot. Please try again later. Check the console for details.',
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  const getFileIcon = (fileName: string) => {
    const extension = fileName.split('.').pop()?.toLowerCase();
    if (extension === 'zip') {
      return <FileArchive className="h-5 w-5 text-blue-500" />;
    }
    return <FileCode className="h-5 w-5 text-green-500" />;
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Upload Your Bot Files</CardTitle>
          <CardDescription>
            You can either select all .h and .cpp files or zip them all together. If you submit via zip, all files should be in the root of the zip file or in
            subfolders. Only C++ related files (.c, .cpp, .cxx, .h, .hpp, .hxx) are allowed.
          </CardDescription>
          <div className="mt-2 text-amber-600 dark:text-amber-400 text-sm">
            <strong>Note:</strong> If you're using macOS, be aware that zip files may contain hidden __MACOSX folders. The server will automatically remove
            these folders, but it's recommended to create your zip files using the terminal command
            <code className="bg-gray-100 dark:bg-gray-800 px-1 rounded">zip -r submission.zip your_files/ -x "*.DS_Store" -x "__MACOSX"</code>
            to avoid potential issues.
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div
            {...getRootProps()}
            className={cn(
              'border-2 border-dashed rounded-lg p-8 text-center cursor-pointer transition-colors',
              isDragActive && !isDragReject && 'border-primary bg-primary/5',
              isDragReject && 'border-destructive bg-destructive/5',
              !isDragActive && !isDragReject && 'border-muted-foreground/25 hover:border-primary/50',
              (isSubmitting || isValidatingZip) && 'opacity-50 pointer-events-none',
              hasZipFile && !isDragActive && 'border-blue-500 bg-blue-50 dark:bg-blue-950/20',
              hasCppFiles && !isDragActive && 'border-green-500 bg-green-50 dark:bg-green-950/20',
            )}
          >
            <input {...getInputProps()} disabled={isSubmitting || isValidatingZip} />
            <div className="flex flex-col items-center justify-center gap-2">
              <Upload className="h-10 w-10 text-muted-foreground" />
              <p className="text-lg font-medium">Click or drag file(s) to this area to upload</p>
              <p className="text-sm text-muted-foreground max-w-md">
                {hasZipFile ? (
                  <span className="text-blue-600 font-medium">You have a zip file. You cannot add more files.</span>
                ) : hasCppFiles ? (
                  <span className="text-green-600 font-medium">You have C++ files. You cannot add a zip file.</span>
                ) : (
                  'Support for a single "zip" file or a list of C++ related files (.c, .cpp, .cxx, .h, .hpp, .hxx).'
                )}{' '}
                Strictly prohibit from uploading other files. If you exploit this, you will be banned. I am logging everything you do. I am always watching. I
                am always listening. I am always waiting. I might even be watching you from behind. Look behind yourself. NOW!
              </p>
            </div>
          </div>

          {files.length > 0 && (
            <div className="mt-4">
              <h3 className="font-medium mb-2">Selected Files ({files.length})</h3>
              <div className="space-y-2 max-h-60 overflow-y-auto p-2 border rounded-md">
                {files.map((file, index) => (
                  <div key={`${file.name}-${index}`} className="flex items-center justify-between p-2 bg-muted/50 rounded">
                    <div className="flex items-center gap-2 truncate">
                      {getFileIcon(file.name)}
                      <span className="truncate">{file.name}</span>
                      <span className="text-xs text-muted-foreground">({(file.size / 1024).toFixed(1)} KB)</span>
                    </div>
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => removeFile(index)}
                      aria-label={`Remove ${file.name}`}
                      disabled={isSubmitting || isValidatingZip}
                    >
                      <X className="h-4 w-4" />
                    </Button>
                  </div>
                ))}
              </div>
            </div>
          )}

          {submitStatus.type && (
            <Alert variant={submitStatus.type === 'error' ? 'destructive' : submitStatus.type === 'info' ? 'default' : 'default'} className="mt-4">
              <AlertTitle className="flex items-center gap-2">
                {submitStatus.type === 'error' ? (
                  <AlertTriangle className="h-4 w-4" />
                ) : submitStatus.type === 'info' ? (
                  <Info className="h-4 w-4" />
                ) : (
                  <CheckCircle className="h-4 w-4" />
                )}
                {submitStatus.type === 'error' ? 'Submission Error' : submitStatus.type === 'info' ? 'Information' : 'Submission Successful'}
              </AlertTitle>
              <AlertDescription>
                <div
                  // font should be monospace
                  className={cn(
                    'text-sm font-mono text-muted-foreground',
                    submitStatus.type === 'error' ? 'text-destructive' : submitStatus.type === 'info' ? 'text-muted-foreground' : 'text-green-500',
                  )}
                  dangerouslySetInnerHTML={{ __html: submitStatus.message || '' }}
                />
              </AlertDescription>
            </Alert>
          )}
        </CardContent>
        <CardFooter>
          <Button onClick={handleSubmit} disabled={isSubmitting || isValidatingZip || files.length === 0} className="w-full sm:w-auto">
            {isSubmitting ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Processing...
              </>
            ) : isValidatingZip ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Validating...
              </>
            ) : (
              'Submit Bot'
            )}
          </Button>
        </CardFooter>
      </Card>
    </div>
  );
}
