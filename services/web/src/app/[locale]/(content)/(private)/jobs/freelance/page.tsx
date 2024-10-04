import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent } from "@/components/ui/card"
import { Check } from "lucide-react"

// Mock data for the selected job
const selectedJob = {
  id: 1,
  title: "Freelance Web Developer",
  company: "TechStartup Inc.",
  location: "Remote",
  postedAgo: "3 days ago",
  image: "/placeholder.svg?height=80&width=80",
  skills: ["React", "Node.js", "TypeScript", "API Integration"],
  description: "We're looking for a skilled freelance web developer to help build a new customer-facing portal. The ideal candidate will have strong experience with React, Node.js, and API integration. This is a 3-month contract with the possibility of extension.",
}

// Mock data for application steps
const applicationSteps = [
  { id: 1, name: "Applied", completed: true },
  { id: 2, name: "Application Accepted", completed: true },
  { id: 3, name: "Job Started", completed: true },
  { id: 4, name: "Job Delivered", completed: false },
  { id: 5, name: "Payment Completed", completed: false },
]

export default function JobsFreelance() {
  return (
    <div className="min-h-[calc(100vh-150px)] bg-gray-100"> 
      <div className="container mx-auto p-4">
        <h1 className="mb-6 text-2xl font-bold">Freelance Job Details</h1>
        <div className="grid gap-6 md:grid-cols-[2fr_1fr]">
          {/* Left side: Job Information */}
          <Card>
            <CardContent className="p-6">
              <div className="mb-4 flex items-center space-x-4">
                <Avatar className="h-16 w-16">
                  <AvatarImage src={selectedJob.image} alt={selectedJob.company} />
                  <AvatarFallback>{selectedJob.company[0]}</AvatarFallback>
                </Avatar>
                <div>
                  <h2 className="text-2xl font-bold">{selectedJob.title}</h2>
                  <p className="text-lg text-gray-500">{selectedJob.company}</p>
                </div>
              </div>
              <div className="mb-4 space-y-2">
                <p className="text-gray-500">{selectedJob.location} • Posted {selectedJob.postedAgo}</p>
              </div>
              <div className="mb-4">
                {selectedJob.skills.map((skill) => (
                  <Badge key={skill} className="mr-2 mb-2">
                    {skill}
                  </Badge>
                ))}
              </div>
              <div className="mb-6">
                <h3 className="mb-2 text-lg font-semibold">Description</h3>
                <p className="text-gray-700">{selectedJob.description}</p>
              </div>
              <div className="rounded-md bg-gray-100 p-4 text-center text-gray-700">
                Please contact the job poster for more information
              </div>
            </CardContent>
          </Card>

          {/* Right side: Application Progress */}
          <Card>
            <CardContent className="p-6">
              <h3 className="mb-4 text-xl font-semibold">Application Progress</h3>
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
                        step.completed ? "bg-green-100 ring-8 ring-white" : "bg-gray-100"
                      }`}
                    >
                      {step.completed && <Check className="h-5 w-5 text-green-500" />}
                    </span>
                    <h3
                      className={`font-medium ${
                        step.completed ? "text-green-500" : "text-gray-500"
                      }`}
                    >
                      {step.name}
                    </h3>
                    {index < applicationSteps.length - 1 && (
                      <div
                        className={`mt-2 -ml-7 h-20 w-1 ${
                          step.completed && applicationSteps[index + 1].completed
                            ? "bg-green-500"
                            : "bg-gray-200"
                        }`}
                      ></div>
                    )}
                  </li>
                ))}
              </ol>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}