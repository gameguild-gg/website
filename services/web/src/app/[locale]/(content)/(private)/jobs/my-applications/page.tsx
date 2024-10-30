'use client'
import { useEffect, useState } from "react"
import { useRouter } from 'next/navigation';
import { getSession } from "next-auth/react"
import { Api, JobTagsApi, JobApplicationsApi } from '@game-guild/apiclient';
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Check, X } from "lucide-react"
import Image from "next/image"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";


const skillTags = [
  "JavaScript", "React", "Node.js", "Python",
  "Java", "C++", "SQL", "NoSQL",
  "AWS", "Docker", "Kubernetes", "Machine Learning"
]

function ProgressBar({ progress, failed }: { progress: number; failed: boolean }) {
  const steps = [0, 1, 2, 3, 4]
  return (
    <div className="flex justify-between mt-2">
      {steps.map((step) => (
        <div
          key={step}
          className={`w-12 h-6 flex items-center justify-center ${
            progress >= step
              ? failed && step === progress
                ? "bg-red-500"
                : "bg-green-500"
              : "bg-gray-300"
          }`}
        >
          {progress >= step && (
            failed && step === progress ? (
              <X className="w-4 h-4 text-white" />
            ) : (
              <Check className="w-4 h-4 text-white" />
            )
          )}
        </div>
      ))}
    </div>
  )
}

export default function MyJobApplications() {
  const [jobTags, setJobTags] = useState<Api.JobTagEntity[] | any>([]);
  const [jobApplications, setJobApplicatons] = useState<Api.JobApplicationEntity[]>([])
  
  const router = useRouter()
  const jobTagsApi = new JobTagsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });
  const jobApplicationApi = new JobApplicationsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  useEffect(() => {
    getAllJobTags();
    loadJobApplications()
  },[]);

  const getAllJobTags = async () => {
    const session: any = await getSession();
    if (!session) {
      router.push('/connect');
      return;
    }
    const response = await jobTagsApi.getManyBaseJobTagControllerJobTagEntity(
      {},
      { headers: { Authorization: `Bearer ${session.user.accessToken}` } },
    );
    if ((response.status = 200)) {
      setJobTags(response.body as Api.JobTagEntity[]||[]);
    }
  };
  
  const loadJobApplications = async () => {
    const session: any = await getSession();
    if (!session) {
      router.push('/connect');
      return;
    }
    const response = await jobApplicationApi.jobApplicationControllerMyApplications(
      { headers: { Authorization: `Bearer ${session.user.accessToken}` } },
    );
    console.log('API Response:\n',response)
    if (response.status == 200 && (response.body as Api.JobApplicationEntity[])?.length >0) {
      setJobApplicatons(response.body as Api.JobApplicationEntity[]);
    } else {
      router.push('/connect');
    }
    //
  }

  return (
    <div className="min-h-[calc(100vh-70px)] bg-gray-100">
      <div className="flex container min-h-full mx-auto p-4 bg-background">
        {/* Sidebar */}
        <aside className="w-[260px] bg-muted p-5 flex flex-col space-y-4">
          <h2 className="text-2xl font-bold">Filter</h2>
          <div>
            <Label htmlFor="jobTitle">Job Title</Label>
            <Input id="jobTitle" placeholder="Enter job title" />
          </div>
          <div>
            <Label htmlFor="location">Location</Label>
            <Input id="location" placeholder="Enter location" />
          </div>
          <div>
            <Label>Tags</Label>
            <div className="grid grid-cols-2 gap-2 mt-2">
              {jobTags.map((tag:Api.JobTagEntity) => (
                <div key={tag.id} className="flex items-center space-x-2">
                  <Checkbox id={tag.id} />
                  <Label htmlFor={tag.name}>{tag.name}</Label>
                </div>
              ))}
            </div>
          </div>
        </aside>

        {/* Main content */}
        <main className="flex-1 p-6">
          <h1 className="text-3xl font-bold mb-6">My Job Applications</h1>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            {jobApplications.length>0 && jobApplications.slice(0, 16).map((app) => (
              <div key={app.id} className="bg-card rounded-lg shadow-md p-4 flex flex-col h-full">
                <div className="flex-grow">
                  <div className="flex items-center mb-2">
                    <Avatar>
                      <AvatarImage src={app?.job?.thumbnail} alt={app?.job?.title} />
                      <AvatarFallback>{app?.job?.title[0]}</AvatarFallback>
                    </Avatar>
                    <span className="font-semibold ml-2">{app?.job?.title}</span>
                  </div>
                  <h3 className="font-bold text-lg mb-2">{app?.job?.title}</h3>
                  <div className="flex flex-wrap gap-1 mb-2">
                    {app?.job?.job_tags.map((tag) => (
                      <Badge key={tag.id} variant="secondary">
                        {tag.name}
                      </Badge>
                    ))}
                  </div>
                </div>
                <div className="mt-auto">
                  <div className="font-bold mt-2">Progress</div>
                  <ProgressBar progress={app.progress} failed={app.rejected} />
                </div>
              </div>
            ))}
          </div>

          {/* Pagination */}
          <div className="flex justify-center mt-8">
            <nav className="flex items-center space-x-2">
              <Button variant="outline" size="icon">
                <span className="sr-only">Go to previous page</span>
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  width="24"
                  height="24"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  className="h-4 w-4"
                >
                  <polyline points="15 18 9 12 15 6" />
                </svg>
              </Button>
              <Button variant="outline" size="sm">
                1
              </Button>
              <Button variant="outline" size="sm">
                2
              </Button>
              <Button variant="outline" size="sm">
                3
              </Button>
              <Button variant="outline" size="icon">
                <span className="sr-only">Go to next page</span>
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  width="24"
                  height="24"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  className="h-4 w-4"
                >
                  <polyline points="9 18 15 12 9 6" />
                </svg>
              </Button>
            </nav>
          </div>
        </main>
      </div>
    </div>
  )
}