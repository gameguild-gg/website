'use client';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { getSession } from 'next-auth/react';
import { Api, JobApplicationsApi } from '@game-guild/apiclient';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Check, X } from 'lucide-react';
import { Button } from '@/components/ui/button';

// Mock data for application steps
const taskStepsNames = [
  'Applied',
  'Application Accepted',
  'Job Started',
  'Job Delivered',
  'Payment Completed',
];

// Mock data for application steps 2
const continuosStepNames = [
  'Applied',
  'Application Accepted',
  'Tests and Interviews',
  'Hiring Proposal Sent',
  'Hiring Process Complete',
];

export default function MyJobApplicationSlug({ params }) {
  const [jobApplication, setJobApplicaton] =
    useState<Api.JobApplicationEntity>();
  const router = useRouter();
  const { jobApplicationSlug } = params;

  const jobApplicationApi = new JobApplicationsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  const timeAgo = (dateString: string): string => {
    const now = new Date();
    const pastDate = new Date(dateString);
    const diff = now.getTime() - pastDate.getTime(); // Time difference in milliseconds

    const seconds = Math.floor(diff / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);
    const months = Math.floor(days / 30);
    const years = Math.floor(days / 365);

    if (years > 0) return `${years} yr${years > 1 ? 's' : ''} ago`;
    if (months > 0) return `${months} mon${months > 1 ? 's' : ''} ago`;
    if (days > 0) return `${days} day${days > 1 ? 's' : ''} ago`;
    if (hours > 0) return `${hours} hr${hours > 1 ? 's' : ''} ago`;
    if (minutes > 0) return `${minutes} min${minutes > 1 ? 's' : ''} ago`;
    return `${seconds} sec${seconds > 1 ? 's' : ''} ago`;
  };

  useEffect(() => {
    loadJobApplication();
  }, []);

  const loadJobApplication = async () => {
    const session: any = await getSession();
    if (!session) {
      router.push('/connect');
      return;
    }
    const response =
      await jobApplicationApi.jobApplicationControllerMyApplicationBySlug(
        jobApplicationSlug,
        { headers: { Authorization: `Bearer ${session.user.accessToken}` } },
      );
    // console.log('API Response.body:\n',response.body)
    if (response.status == 200) {
      setJobApplicaton(response.body as Api.JobApplicationEntity);
    } else {
      // router.push('/connect');
    }
    //
  };

  return (
    <div className="min-h-[calc(100vh-150px)] bg-gray-100">
      <div className="container mx-auto p-4">
        <div className="flex justify-between">
          <h1 className="mb-6 text-2xl font-bold">Job Details</h1>
          <Button
            variant="outline"
            onClick={() => router.push('/jobs/my-applications')}
          >
            Return
          </Button>
        </div>
        {jobApplication ? (
          <div className="grid gap-6 md:grid-cols-[2fr_1fr]">
            {/* Left side: Job Information */}
            <Card>
              <CardContent className="p-6 h-full">
                <div className="mb-4 flex items-center space-x-4">
                  <Avatar className="h-16 w-16">
                    <AvatarImage
                      // src={jobApplication?.job?.thumbnail}
                      alt={jobApplication?.job?.title[0]}
                    />
                    <AvatarFallback>
                      {jobApplication?.job?.title[0]}
                    </AvatarFallback>
                  </Avatar>
                  <div>
                    <h2 className="text-2xl font-bold">
                      {jobApplication?.job?.title}
                    </h2>
                    <p className="text-lg text-gray-500">
                      {jobApplication?.job?.owner?.username}
                    </p>
                  </div>
                </div>
                <div className="mb-4 space-y-2">
                  <p className="text-gray-500">
                    {jobApplication?.job?.location} • Posted{' '}
                    {timeAgo(jobApplication?.job?.createdAt ?? '')} • Applied{' '}
                    {timeAgo(jobApplication?.createdAt ?? '')}
                  </p>
                </div>
                <div className="mb-4">
                  {jobApplication?.job?.job_tags.map((tag) => (
                    <Badge key={tag.id} className="mr-2 mb-2">
                      {tag.name}
                    </Badge>
                  ))}
                </div>
                <div className="mb-6">
                  <h3 className="mb-2 text-lg font-semibold">Description</h3>
                  <p className="text-gray-700">{jobApplication?.job?.body}</p>
                </div>
                <div className="rounded-md bg-gray-100 p-4 text-center text-gray-700">
                  Please contact the job poster for more information
                </div>
              </CardContent>
            </Card>
            {/* Right side: Application Progress */}
            <Card>
              <CardContent className="p-6">
                <h3 className="mb-4 text-xl font-semibold">
                  Application Progress
                </h3>
                <ol className="relative ml-4">
                  {jobApplication.progress &&
                    [0, 1, 2, 3, 4].map((step, index) => (
                      <li key={step} className={`mb-1 ml-6`}>
                        <span
                          className={`absolute -left-4 flex h-8 w-8 items-center justify-center rounded-full ${
                            jobApplication.progress >= step
                              ? 'bg-green-100 ring-8 ring-white'
                              : jobApplication.progress == step - 1 &&
                                  jobApplication.rejected
                                ? 'bg-red-100'
                                : 'bg-gray-100'
                          }`}
                        >
                          {jobApplication.progress >= step && (
                            <Check className="h-5 w-5 text-green-500" />
                          )}
                          {jobApplication.progress == step - 1 &&
                            jobApplication.rejected && (
                              <X className="h-5 w-5 text-red-500" />
                            )}
                        </span>
                        <h3
                          className={`font-medium ${
                            jobApplication.progress >= step
                              ? 'text-green-500'
                              : 'text-gray-500'
                          }`}
                        >
                          {jobApplication.job.job_type == 'TASK'
                            ? taskStepsNames[step]
                            : continuosStepNames[step]}
                        </h3>
                        {/* Thin green bar */}
                        {index < 4 && (
                          <div
                            className={`mt-2 -ml-7 h-20 w-1 ${
                              jobApplication.progress >= step + 1
                                ? 'bg-green-500'
                                : jobApplication.progress == step &&
                                    jobApplication.rejected
                                  ? 'bg-red-500'
                                  : 'bg-gray-200'
                            }`}
                          ></div>
                        )}
                      </li>
                    ))}
                </ol>
              </CardContent>
            </Card>
          </div>
        ) : (
          <Card>
            <CardContent className="p-6">
              Job Application Not Found.
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  );
}
