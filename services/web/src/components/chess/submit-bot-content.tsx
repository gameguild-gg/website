'use client';

import { useCallback, useState } from 'react';
import { useDropzone } from 'react-dropzone';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/chess/ui/card';
import { Button } from '@/components/chess/ui/button';
import { Alert, AlertDescription, AlertTitle } from '@/components/chess/ui/alert';
import { AlertTriangle, CheckCircle, FileArchive, FileCode, Info, Loader2, Upload, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import JSZip from 'jszip';

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
          message: `Your zip file contains invalid file types: ${invalidFiles.join(', ')}. Only C++ related files (.c, .cpp, .cxx, .h, .hpp, .hxx) are allowed.`,
        };
      }

      return { valid: true };
    } catch (error) {
      console.error('Error validating zip:', error);
      return {
        valid: false,
        message: 'Failed to process zip file. The file may be corrupted or invalid.',
      };
    } finally {
      setIsValidatingZip(false);
      setSubmitStatus({ type: null, message: null });
    }
  };

  const onDrop = useCallback(async (acceptedFiles: File[]) => {
    // Reset status when new files are added
    setSubmitStatus({ type: null, message: null });

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
  }, []);

  const { getRootProps, getInputProps, isDragActive, isDragReject } = useDropzone({
    onDrop,
    accept: {
      'text/x-c': ['.c'],
      'text/x-c++src': ['.cpp', '.cxx'],
      'text/x-h': ['.h', '.hpp', '.hxx'],
      'application/zip': ['.zip'],
    },
    multiple: true,
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

    try {
      const formData = new FormData();
      files.forEach((file) => {
        formData.append('files', file);
      });

      const response = await fetch('/api/submit-bot', {
        method: 'POST',
        body: formData,
      });

      const data = await response.json();

      if (response.ok) {
        setSubmitStatus({
          type: 'success',
          message: data.message,
        });
        // Clear files on successful submission
        setFiles([]);
      } else {
        setSubmitStatus({
          type: 'error',
          message: data.message || 'An error occurred during submission.',
        });
      }
    } catch (error) {
      setSubmitStatus({
        type: 'error',
        message: 'Failed to submit bot. Please try again later.',
      });
      console.error('Submission error:', error);
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
            )}
          >
            <input {...getInputProps()} disabled={isSubmitting || isValidatingZip} />
            <div className="flex flex-col items-center justify-center gap-2">
              <Upload className="h-10 w-10 text-muted-foreground" />
              <p className="text-lg font-medium">Click or drag file(s) to this area to upload</p>
              <p className="text-sm text-muted-foreground max-w-md">
                Support for a single "zip" file or a list of C++ related files (.c, .cpp, .cxx, .h, .hpp, .hxx). Strictly prohibit from uploading other files.
                If you exploit this, you will be banned. I am logging everything you do. I am always watching. I am always listening. I am always waiting. I
                might even be watching you from behind. Look behind yourself. NOW!
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
              <AlertDescription>{submitStatus.message}</AlertDescription>
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
