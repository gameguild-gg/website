import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Check, X } from "lucide-react"
import Image from "next/image"

const skillTags = [
  "JavaScript", "React", "Node.js", "Python",
  "Java", "C++", "SQL", "NoSQL",
  "AWS", "Docker", "Kubernetes", "Machine Learning"
]

const jobApplications = Array(20).fill(null).map((_, i) => ({
  id: i + 1,
  companyName: `Company ${i + 1}`,
  jobTitle: `Software Engineer ${i + 1}`,
  tags: skillTags.slice(0, Math.floor(Math.random() * 5) + 1),
  progress: Math.floor(Math.random() * 6),
  failed: Math.random() > 0.8
}))

function ProgressBar({ progress, failed }: { progress: number; failed: boolean }) {
  const steps = [1, 2, 3, 4, 5]
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
  // 
  return (
    <div className="flex min-h-screen bg-background">
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
          <Label>Skill Tags</Label>
          <div className="grid grid-cols-2 gap-2 mt-2">
            {skillTags.map((tag) => (
              <div key={tag} className="flex items-center space-x-2">
                <Checkbox id={tag} />
                <Label htmlFor={tag}>{tag}</Label>
              </div>
            ))}
          </div>
        </div>
      </aside>

      {/* Main content */}
      <main className="flex-1 p-6">
        <h1 className="text-3xl font-bold mb-6">My Job Applications</h1>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
          {jobApplications.slice(0, 16).map((job) => (
            <div key={job.id} className="bg-card rounded-lg shadow-md p-4 flex flex-col h-full">
              <div className="flex-grow">
                <div className="flex items-center mb-2">
                  <Image
                    src="/placeholder.svg?height=40&width=40"
                    alt={`${job.companyName} logo`}
                    width={40}
                    height={40}
                    className="rounded-full mr-2"
                  />
                  <span className="font-semibold">{job.companyName}</span>
                </div>
                <h3 className="font-bold text-lg mb-2">{job.jobTitle}</h3>
                <div className="flex flex-wrap gap-1 mb-2">
                  {job.tags.map((tag) => (
                    <Badge key={tag} variant="secondary">
                      {tag}
                    </Badge>
                  ))}
                </div>
              </div>
              <div className="mt-auto">
                <div className="font-bold mt-2">Progress</div>
                <ProgressBar progress={job.progress} failed={job.failed} />
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
  )
}