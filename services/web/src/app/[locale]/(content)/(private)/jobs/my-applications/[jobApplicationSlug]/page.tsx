'use client'
import { useRouter } from 'next/navigation';
import { Api, JobApplicationsApi } from '@game-guild/apiclient';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Check } from 'lucide-react';
import { useEffect, useState } from 'react';
import { getSession } from 'next-auth/react';


// Mock data for the selected job
const selectedJob = {
  id: 1,
  title: 'Freelance Web Developer',
  company: 'TechStartup Inc.',
  location: 'Remote',
  postedAgo: '3 days ago',
  image: '/assets/images/placeholder.svg?height=80&width=80',
  skills: ['React', 'Node.js', 'TypeScript', 'API Integration'],
  description:
    "We're looking for a skilled freelance web developer to help build a new customer-facing portal. The ideal candidate will have strong experience with React, Node.js, and API integration. This is a 3-month contract with the possibility of extension.",
};

// Mock data for application steps
const applicationSteps = [
  { id: 1, name: 'Applied', completed: true },
  { id: 2, name: 'Application Accepted', completed: true },
  { id: 3, name: 'Job Started', completed: true },
  { id: 4, name: 'Job Delivered', completed: false },
  { id: 5, name: 'Payment Completed', completed: false },
];

// Mock data for application steps 2
const applicationSteps2 = [
  { id: 1, name: 'Applied', completed: true },
  { id: 2, name: 'Application Accepted', completed: true },
  { id: 3, name: 'Tests and Interviews', completed: true },
  { id: 4, name: 'Hiring Proposal Sent', completed: false },
  { id: 5, name: 'Hiring Process Complete', completed: false },
];

export default function MyJobApplicationSlug({ params }) {
  const [jobApplication, setJobApplicaton] = useState<Api.JobApplicationEntity>()
  const router = useRouter()
  const { jobApplicationSlug } = params
  
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
    loadJobApplication()
  },[]);

  const loadJobApplication = async () => {
    const session: any = await getSession();
    // console.log("MY APPLICATIONS SLUG. SESSION: ",session)
    if (!session) {
      router.push('/connect');
      return;
    }
    const response = await jobApplicationApi.jobApplicationControllerMyApplicationBySlug(
      jobApplicationSlug,
      { headers: { Authorization: `Bearer ${session.user.accessToken}` } },
    );
    console.log('API Response.body:\n',response.body)
    if (response.status == 200) {
      setJobApplicaton(response.body as Api.JobApplicationEntity);
    } else {
      //router.push('/connect');
    }
    //
  }
  

  return (
    <div className="min-h-[calc(100vh-150px)] bg-gray-100">
      <div className="container mx-auto p-4">
        <h1 className="mb-6 text-2xl font-bold">Job Details</h1>
        {jobApplication ? (
          <div className="grid gap-6 md:grid-cols-[2fr_1fr]">
            {/* Left side: Job Information */}
            <Card>
              <CardContent className="p-6">
                <div className="mb-4 flex items-center space-x-4">
                  <Avatar className="h-16 w-16">
                    <AvatarImage
                      src={jobApplication?.job?.thumbnail}
                      alt={jobApplication?.job?.title[0]}
                    />
                    <AvatarFallback>{jobApplication?.job?.title[0]}</AvatarFallback>
                  </Avatar>
                  <div>
                    <h2 className="text-2xl font-bold">{jobApplication?.job?.title}</h2>
                    <p className="text-lg text-gray-500">{jobApplication?.job?.owner?.username}</p>
                  </div>
                </div>
                <div className="mb-4 space-y-2">
                  <p className="text-gray-500">
                    {jobApplication?.job?.location} • Posted {timeAgo(jobApplication.job.createdAt)} • Applied {timeAgo(jobApplication?.createdAt)}
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
                <ol className="relative ">
                  {applicationSteps.map((step, index) => (
                    <li
                      key={step.id}
                      className={`
                      mb-1 ml-6
                      `}
                    >
                      <span
                        className={`absolute -left-4 flex h-8 w-8 items-center justify-center rounded-full ${
                          step.completed
                            ? 'bg-green-100 ring-8 ring-white'
                            : 'bg-gray-100'
                        }`}
                      >
                        {step.completed && (
                          <Check className="h-5 w-5 text-green-500" />
                        )}
                      </span>
                      <h3
                        className={`font-medium ${
                          step.completed ? 'text-green-500' : 'text-gray-500'
                        }`}
                      >
                        {step.name}
                      </h3>
                      {index < applicationSteps.length - 1 && (
                        <div
                          className={`mt-2 -ml-7 h-20 w-1 ${
                            step.completed &&
                            applicationSteps[index + 1].completed
                              ? 'bg-green-500'
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
        ):(
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
