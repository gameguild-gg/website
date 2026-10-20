'use client';

import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@game-guild/ui/components/dropdown-menu';
import { Progress } from '@game-guild/ui/components/progress';
import { useCallback, useEffect, useState } from 'react';
// import { Tabs, TabsContent, TabsList, TabsTrigger } from '@game-guild/ui/components/tabs';
import { CourseCompletionCertificateService } from '@/lib/courses/services/certificate.service';
import { ContentReportService } from '@/lib/courses/services/content-report.service';
import { CertificateNotification, ContentNavigationSidebar, LessonViewer, ReportContentDialog } from '@game-guild/courses/components/learning';
import { BookOpen, CheckCircle, ChevronLeft, ChevronRight, Clock, FileText, Flag, MessageSquare, MoreVertical, Play, Trophy, Upload } from 'lucide-react';
import { ActivityComponent } from './activity-component';
import { PeerReviewInterface } from './peer-review-interface';

type ContentReportMessage = {
  type: 'success' | 'error';
  text: string;
};

interface ContentItem {
  id: string;
  title: string;
  type: 'lesson' | 'activity' | 'quiz' | 'assignment' | 'peer-review';
  status: 'locked' | 'available' | 'in-progress' | 'completed';
  duration?: number; // in minutes
  description?: string;
  order: number;
  isRequired: boolean;
  activityType?: 'text' | 'code' | 'file' | 'quiz' | 'discussion';
  content?: unknown;
  progress?: number;
  score?: number;
  maxScore?: number;
}

interface Module {
  id: string;
  title: string;
  description: string;
  order: number;
  items: ContentItem[];
  isLocked: boolean;
  progress: number;
}

interface CourseData {
  id: string;
  title: string;
  description: string;
  modules: Module[];
  overallProgress: number;
  totalItems: number;
  completedItems: number;
  currentItem?: ContentItem;
  estimatedTimeToComplete: number;
}

interface CourseContentViewerProps {
  courseSlug: string;
}

export function CourseContentViewer({ courseSlug }: CourseContentViewerProps) {
  const [courseData, setCourseData] = useState<CourseData | null>(null);
  const [currentItem, setCurrentItem] = useState<ContentItem | null>(null);
  const [loading, setLoading] = useState(true);
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [showReportDialog, setShowReportDialog] = useState(false);
  const [reportMessage, setReportMessage] = useState<ContentReportMessage | null>(null);
  const [certificateEligibility, setCertificateEligibility] = useState<any>(null);
  const [showCertificateNotification, setShowCertificateNotification] = useState(false);

  const loadCourseData = useCallback(async () => {
    try {
      setLoading(true);
      const { getCourseLearningData } = await import('@/lib/courses/server-actions');
      const data = await getCourseLearningData(courseSlug);

      if (!data) {
        setCourseData(null);
        setCurrentItem(null);
        return;
      }

      setCourseData(data as CourseData);
      setCurrentItem((data.currentItem as ContentItem | undefined) ?? null);
    } catch (error) {
      console.error('Error loading course data:', error);
    } finally {
      setLoading(false);
    }
  }, [courseSlug]);

  useEffect(() => {
    loadCourseData();
  }, [loadCourseData]);

  const handleItemSelect = (item: ContentItem) => {
    if (item.status === 'locked') return;
    setCurrentItem(item);
    setReportMessage(null);
  };

  const handleItemComplete = async (itemId: string, score?: number) => {
    if (!courseData) return;

    if (currentItem?.type === 'lesson') {
      try {
        const { markContentComplete } = await import('@/lib/courses/server-actions');
        await markContentComplete(courseData.id, itemId);
      } catch (error) {
        console.error('Error syncing lesson completion:', error);
      }
    }

    // Update the item status and course progress
    const updatedCourseData = { ...courseData };
    let itemFound = false;

    for (const module of updatedCourseData.modules) {
      for (const item of module.items) {
        if (item.id === itemId) {
          item.status = 'completed';
          item.progress = 100;
          if (score !== undefined) {
            item.score = score;
          }
          itemFound = true;
          break;
        }
      }
      if (itemFound) break;
    }

    // Recalculate progress
    const totalItems = updatedCourseData.modules.reduce((sum, module) => sum + module.items.length, 0);
    const completedItems = updatedCourseData.modules.reduce((sum, module) => sum + module.items.filter((item) => item.status === 'completed').length, 0);

    updatedCourseData.overallProgress = Math.round((completedItems / totalItems) * 100);
    updatedCourseData.completedItems = completedItems;

    // Update module progress
    for (const module of updatedCourseData.modules) {
      const moduleCompleted = module.items.filter((item) => item.status === 'completed').length;
      module.progress = Math.round((moduleCompleted / module.items.length) * 100);
    }

    setCourseData(updatedCourseData);

    // Check for certificate eligibility when course is 100% complete
    if (updatedCourseData.overallProgress === 100) {
      try {
        const result = await CourseCompletionCertificateService.handleCourseCompletion(
          courseData.id,
          updatedCourseData,
          'Current Student', // In real app, get from user context
        );

        if (result.showCertificateNotification) {
          setCertificateEligibility(result.eligibility);
          setShowCertificateNotification(true);
        }
      } catch (error) {
        console.error('Error checking certificate eligibility:', error);
      }
    }

    // Move to next available item
    const nextItem = findNextAvailableItem(updatedCourseData, itemId);
    if (nextItem) {
      setCurrentItem(nextItem);
    }
  };

  const findNextAvailableItem = (data: CourseData, currentItemId: string): ContentItem | null => {
    let foundCurrent = false;

    for (const module of data.modules) {
      for (const item of module.items) {
        if (foundCurrent && (item.status === 'available' || item.status === 'in-progress')) {
          return item;
        }
        if (item.id === currentItemId) {
          foundCurrent = true;
        }
      }
    }

    return null;
  };

  const navigateToItem = (direction: 'prev' | 'next') => {
    if (!courseData || !currentItem) return;

    const allItems: ContentItem[] = [];
    courseData.modules.forEach((moduleItem) => {
      allItems.push(...moduleItem.items);
    });

    const currentIndex = allItems.findIndex((item) => item.id === currentItem.id);

    if (direction === 'prev' && currentIndex > 0) {
      const prevItem = allItems[currentIndex - 1];
      if (prevItem && prevItem.status !== 'locked') {
        setCurrentItem(prevItem);
      }
    } else if (direction === 'next' && currentIndex < allItems.length - 1) {
      const nextItem = allItems[currentIndex + 1];
      if (nextItem && nextItem.status !== 'locked') {
        setCurrentItem(nextItem);
      }
    }
  };

  const handleGenerateCertificate = async () => {
    if (!certificateEligibility) return;

    try {
      const result = await CourseCompletionCertificateService.generateCertificate(
        certificateEligibility.courseId,
        certificateEligibility.userId ?? '',
        {
          templateId: certificateEligibility.templateId,
          enrollmentId: certificateEligibility.enrollmentId,
        },
      );

      if (result.success) {
        setCertificateEligibility((current: any) => ({
          ...current,
          certificateId: result.certificateId,
          certificateUrl: result.certificateUrl,
        }));

        if (result.certificateUrl) {
          window.open(result.certificateUrl, '_blank', 'noopener,noreferrer');
        }
      } else {
        console.error('Failed to generate certificate:', result.error);
      }
    } catch (error) {
      console.error('Error generating certificate:', error);
    }
  };

  const handleViewCertificate = () => {
    if (certificateEligibility?.certificateUrl) {
      window.open(certificateEligibility.certificateUrl, '_blank', 'noopener,noreferrer');
    }
  };

  const handleReportContent = async (contentId: string, reason: string, description: string) => {
    try {
      if (!currentItem) return;

      const report = await ContentReportService.createReport({
        contentId,
        contentTitle: currentItem.title,
        contentType: currentItem.type || 'content',
        reportType: reason,
        reason,
        description,
      });

      if (!report.success) {
        setReportMessage({
          type: 'error',
          text: report.error || 'The report could not be submitted. Review the details and try again.',
        });
        return;
      }

      setReportMessage({
        type: 'success',
        text: report.message || 'Report submitted for moderation.',
      });
    } catch (error) {
      console.error('Failed to report content:', error);
      setReportMessage({
        type: 'error',
        text: 'The report could not be submitted. Review the details and try again.',
      });
    }
  };

  const getContentIcon = (type: string) => {
    switch (type) {
      case 'lesson':
        return <BookOpen className="h-4 w-4" />;
      case 'activity':
        return <Play className="h-4 w-4" />;
      case 'quiz':
        return <FileText className="h-4 w-4" />;
      case 'assignment':
        return <Upload className="h-4 w-4" />;
      case 'peer-review':
        return <MessageSquare className="h-4 w-4" />;
      default:
        return <BookOpen className="h-4 w-4" />;
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-gray-950 text-white">
        <div className="container mx-auto px-4 py-8">
          <div className="flex items-center justify-center h-64">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
          </div>
        </div>
      </div>
    );
  }

  if (!courseData) {
    return (
      <div className="min-h-screen bg-gray-950 text-white">
        <div className="container mx-auto px-4 py-8">
          <Card className="bg-gray-800 border-gray-700">
            <CardContent className="p-6">
              <p className="text-center text-gray-400">Course not found</p>
            </CardContent>
          </Card>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-950 text-white">
      <div className="border-b border-gray-800 bg-gray-900">
        <div className="container mx-auto px-4 py-4">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-2xl font-bold">{courseData.title}</h1>
              <div className="flex items-center gap-4 mt-2 text-sm text-gray-400">
                <span className="flex items-center gap-1">
                  <CheckCircle className="h-4 w-4" />
                  {courseData.completedItems}/{courseData.totalItems} Complete
                </span>
                <span className="flex items-center gap-1">
                  <Clock className="h-4 w-4" />
                  {courseData.estimatedTimeToComplete}h remaining
                </span>
                <span className="flex items-center gap-1">
                  <Trophy className="h-4 w-4" />
                  {courseData.overallProgress}% Progress
                </span>
              </div>
            </div>
            <Button variant="outline" size="sm" onClick={() => setSidebarOpen(!sidebarOpen)} className="border-gray-600">
              {sidebarOpen ? 'Hide' : 'Show'} Contents
            </Button>
          </div>
          <Progress value={courseData.overallProgress} className="mt-4 h-2" />
        </div>
      </div>

      <div className="flex">
        {sidebarOpen && (
          <ContentNavigationSidebar
            courseId={courseData.id}
            modules={courseData.modules.map((module) => ({
              ...module,
              contentItems: module.items,
            }))}
            currentContentId={currentItem?.id}
            onContentSelect={(contentId: string) => {
              // Find the item by id across all modules
              const foundItem = courseData.modules.flatMap((module) => module.items).find((item) => item.id === contentId);
              if (foundItem) {
                handleItemSelect(foundItem);
              }
            }}
          />
        )}

        <main className={`flex-1 ${sidebarOpen ? 'ml-80' : ''}`}>
          <div className="container mx-auto px-4 py-8">
            {/* Certificate Notification */}
            {showCertificateNotification && certificateEligibility && (
              <div className="mb-6">
                <CertificateNotification
                  courseId={certificateEligibility.courseId}
                  courseTitle={certificateEligibility.courseTitle}
                  completionDate={certificateEligibility.completedAt.toISOString()}
                  studentName="Current Student"
                  finalGrade={certificateEligibility.finalGrade}
                  onGenerateCertificate={handleGenerateCertificate}
                  onViewCertificate={handleViewCertificate}
                />
              </div>
            )}

            {currentItem && (
              <>
                {/* Navigation Controls */}
                <div className="flex items-center justify-between mb-6">
                  <Button variant="outline" size="sm" onClick={() => navigateToItem('prev')} className="border-gray-600">
                    <ChevronLeft className="h-4 w-4 mr-1" />
                    Previous
                  </Button>

                  <div className="flex items-center gap-2">
                    {getContentIcon(currentItem.type)}
                    <Badge variant="outline" className="border-gray-600">
                      {currentItem.type.charAt(0).toUpperCase() + currentItem.type.slice(1)}
                    </Badge>
                    {currentItem.isRequired && <Badge variant="secondary">Required</Badge>}
                  </div>

                  <Button variant="outline" size="sm" onClick={() => navigateToItem('next')} className="border-gray-600">
                    Next
                    <ChevronRight className="h-4 w-4 ml-1" />
                  </Button>
                </div>

                {/* Content Area */}
                <Card className="bg-gray-800 border-gray-700">
                  <CardHeader>
                    <CardTitle className="flex items-center justify-between">
                      <span>{currentItem.title}</span>
                      <div className="flex items-center gap-2">
                        {currentItem.duration && (
                          <span className="text-sm font-normal text-gray-400 flex items-center gap-1">
                            <Clock className="h-4 w-4" />
                            {currentItem.duration} min
                          </span>
                        )}
                        <DropdownMenu>
                          <DropdownMenuTrigger render={<Button variant="ghost" size="sm" aria-label="Content actions" />}>
                            <MoreVertical className="h-4 w-4" />
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuItem onClick={() => setShowReportDialog(true)}>
                              <Flag className="h-4 w-4 mr-2" />
                              Report Content
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </div>
                    </CardTitle>
                    {currentItem.description && <p className="text-gray-400">{currentItem.description}</p>}
                  </CardHeader>
                  <CardContent>
                    {reportMessage && (
                      <div
                        role={reportMessage.type === 'success' ? 'status' : 'alert'}
                        className={`mb-4 rounded-md border px-4 py-3 text-sm ${
                          reportMessage.type === 'success'
                            ? 'border-emerald-500/40 bg-emerald-500/10 text-emerald-100'
                            : 'border-red-500/40 bg-red-500/10 text-red-100'
                        }`}
                      >
                        {reportMessage.text}
                      </div>
                    )}
                    {currentItem.type === 'lesson' ? (
                      <LessonViewer item={currentItem} onComplete={() => handleItemComplete(currentItem.id)} />
                    ) : currentItem.type === 'peer-review' ? (
                      <PeerReviewInterface item={currentItem} courseId={courseData.id} onComplete={(score?: number) => handleItemComplete(currentItem.id, score)} />
                    ) : (
                      <ActivityComponent item={currentItem} courseId={courseData.id} onComplete={(score?: number) => handleItemComplete(currentItem.id, score)} />
                    )}
                  </CardContent>
                </Card>

                {/* Report Content Dialog */}
                <ReportContentDialog open={showReportDialog} onOpenChange={setShowReportDialog} contentId={currentItem.id} contentTitle={currentItem.title} onSubmit={handleReportContent} />
              </>
            )}
          </div>
        </main>
      </div>
    </div>
  );
}
