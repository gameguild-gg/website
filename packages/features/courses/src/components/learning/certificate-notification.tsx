'use client';

import { useState } from 'react';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Card, CardContent, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@game-guild/ui/components/dialog';
import { Award, Check, Download, ExternalLink, Loader2 } from 'lucide-react';

interface CertificateNotificationProps {
  readonly courseId: string;
  readonly courseTitle: string;
  readonly completionDate: string;
  readonly studentName: string;
  readonly finalGrade?: number;
  readonly onGenerateCertificate?: () => void;
  readonly onViewCertificate?: () => void;
}

export function CertificateNotification({ courseId, courseTitle, completionDate, studentName, finalGrade, onGenerateCertificate, onViewCertificate }: CertificateNotificationProps) {
  const [isGenerating, setIsGenerating] = useState(false);
  const [showPreview, setShowPreview] = useState(false);

  const handleGenerateCertificate = async () => {
    setIsGenerating(true);
    try {
      if (onGenerateCertificate) {
         onGenerateCertificate();
      }
    } finally {
      setIsGenerating(false);
    }
  };

  const handleViewCertificate = () => {
    if (onViewCertificate) {
      onViewCertificate();
    }
  };

  return (
    <>
      <Card data-course-id={courseId} className="w-full border-green-200 bg-green-50/50 dark:bg-green-950/20 dark:border-green-800">
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle className="flex items-center gap-2 text-green-800 dark:text-green-400">
              <Award className="h-6 w-6" />
              Certificate Available!
            </CardTitle>
            <Badge className="bg-green-500 text-white">
              <Check className="h-3 w-3 mr-1" />
              Course Completed
            </Badge>
          </div>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="text-center space-y-2">
            <h3 className="text-xl font-semibold text-green-800 dark:text-green-400">Congratulations, {studentName}!</h3>
            <p className="text-green-700 dark:text-green-300">
              You have successfully completed <strong>{courseTitle}</strong>
            </p>
            {finalGrade && (
              <p className="text-sm text-green-600 dark:text-green-400">
                Final Grade: <span className="font-mono font-semibold">{finalGrade}%</span>
              </p>
            )}
            <p className="text-xs text-green-600 dark:text-green-400">Completed on {new Date(completionDate).toLocaleDateString()}</p>
          </div>

          <div className="flex flex-col sm:flex-row gap-3 justify-center">
            <Button onClick={handleGenerateCertificate} disabled={isGenerating} className="bg-green-600 hover:bg-green-700 text-white">
              {isGenerating ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  Generating Certificate...
                </>
              ) : (
                <>
                  <Award className="h-4 w-4 mr-2" />
                  Generate Certificate
                </>
              )}
            </Button>

            <Button variant="outline" onClick={() => setShowPreview(true)}>
              <ExternalLink className="h-4 w-4 mr-2" />
              Preview
            </Button>

            <Button variant="outline" onClick={handleViewCertificate}>
              <Download className="h-4 w-4 mr-2" />
              View & Download
            </Button>
          </div>

          <div className="bg-green-100 dark:bg-green-900/30 p-4 rounded-lg">
            <h4 className="font-semibold text-green-800 dark:text-green-400 mb-2">What's Next?</h4>
            <ul className="text-sm text-green-700 dark:text-green-300 space-y-1">
              <li>• Share your achievement on social media</li>
              <li>• Add this certificate to your professional profile</li>
              <li>• Explore more advanced courses in our catalog</li>
              <li>• Join our community to connect with fellow learners</li>
            </ul>
          </div>
        </CardContent>
      </Card>

      {/* Certificate Preview Dialog */}
      <Dialog open={showPreview} onOpenChange={setShowPreview}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Certificate Preview</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="bg-gradient-to-br from-blue-50 to-purple-50 dark:from-blue-950 dark:to-purple-950 p-8 rounded-lg border-2 border-gray-200 dark:border-gray-700">
              <div className="text-center space-y-4">
                <div className="flex justify-center">
                  <Award className="h-16 w-16 text-yellow-500" />
                </div>
                <h2 className="text-2xl font-bold text-gray-900 dark:text-gray-100">Certificate of Completion</h2>
                <p className="text-lg text-gray-700 dark:text-gray-300">This certifies that</p>
                <h3 className="text-xl font-semibold text-blue-600 dark:text-blue-400">{studentName}</h3>
                <p className="text-gray-700 dark:text-gray-300">has successfully completed</p>
                <h4 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{courseTitle}</h4>
                {finalGrade && <p className="text-sm text-gray-600 dark:text-gray-400">with a final grade of {finalGrade}%</p>}
                <p className="text-sm text-gray-500 dark:text-gray-500">Date of Completion: {new Date(completionDate).toLocaleDateString()}</p>
                <div className="pt-4 border-t border-gray-200 dark:border-gray-700">
                  <p className="text-sm text-gray-600 dark:text-gray-400">Game Guild Academy</p>
                </div>
              </div>
            </div>
            <div className="flex justify-center gap-2">
              <Button onClick={handleGenerateCertificate} disabled={isGenerating}>
                {isGenerating ? (
                  <>
                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                    Generating...
                  </>
                ) : (
                  'Generate Official Certificate'
                )}
              </Button>
              <Button variant="outline" onClick={() => setShowPreview(false)}>
                Close Preview
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}
