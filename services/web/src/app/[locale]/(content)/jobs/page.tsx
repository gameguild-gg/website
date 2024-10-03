"use client"

import { useState } from "react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Card, CardContent } from "@/components/ui/card"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Check, Search } from "lucide-react"
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "@/components/ui/command"
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover"

// Mock data for jobs
const jobs = [
  {
    id: 1,
    title: "Frontend Developer",
    company: "TechCorp",
    location: "San Francisco, CA",
    postedAgo: "2 days ago",
    image: "/placeholder.svg?height=40&width=40",
    skills: ["React", "TypeScript", "CSS"],
    description: "We are looking for a skilled Frontend Developer to join our team...\nLorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
  },
  {
    id: 2,
    title: "Backend Engineer",
    company: "DataSystems",
    location: "Remote",
    postedAgo: "1 week ago",
    image: "/placeholder.svg?height=40&width=40",
    skills: ["Node.js", "Python", "MongoDB"],
    description: "Seeking an experienced Backend Engineer to develop scalable services...\nLorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
  },
  {
    id: 3,
    title: "UX Designer",
    company: "CreativeMinds",
    location: "New York, NY",
    postedAgo: "3 days ago",
    image: "/placeholder.svg?height=40&width=40",
    skills: ["Figma", "User Research", "Prototyping"],
    description: "Join our design team to create intuitive and engaging user experiences...\nLorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
  },
  {
    id: 5,
    title: "Game Designer",
    company: "CreativeMinds",
    location: "New York, NY",
    postedAgo: "3 days ago",
    image: "/placeholder.svg?height=40&width=40",
    skills: ["Figma", "User Research", "Prototyping"],
    description: "Join our design team to create intuitive and engaging user experiences...\nLorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
  },
]

// Mock data for job types
const types = [
    { value: "fulltime", label: "Full Time" },
    { value: "fte", label: "FTE" },
    { value: "project", label: "Project-Based" },
    { value: "hourly", label: "Hourly" },
  ]

// Mock data for job skill types
const tags = [
  { value: "react", label: "React" },
  { value: "typescript", label: "TypeScript" },
  { value: "nodejs", label: "Node.js" },
  { value: "python", label: "Python" },
  { value: "ux", label: "UX Design" },
]

export default function JobBoard() {
  const [selectedJob, setSelectedJob] = useState(jobs[0])
  const [selectedJobTypes, setSelectedJobTypes] = useState<string[]>([])
  const [selectedTags, setSelectedTags] = useState<string[]>([])

  const getLabelsByValue = (tags:{value:string, label:string}[], values:string[]) :string[] => {
    return values.map(value => tags.find(tag => tag.value === value)?.label || '');
  }

  return (
    <div className="min-h-[calc(100vh-150px)] bg-gray-100">
      <div className="container mx-auto p-4">
        <div className="mb-6 flex space-x-4">
          <div className="relative flex-grow">
            <Input className="pl-10" placeholder="Search jobs..." />
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 transform text-gray-400" />
          </div>
          {/* Job Type */}
          <Popover>
            <PopoverTrigger asChild>
              <Button variant="outline" className="w-[250px] justify-start">
                {selectedJobTypes.length > 0
                  ? `${getLabelsByValue(types, selectedJobTypes)}`
                  : "Job Types"}
              </Button>
            </PopoverTrigger>
            <PopoverContent className="w-[200px] p-0">
              <Command>
                <CommandInput placeholder="Search type..." />
                <CommandList>
                  <CommandEmpty>No tags found.</CommandEmpty>
                  <CommandGroup>
                    {types.map((type) => (
                      <CommandItem
                        key={type.value}
                        onSelect={() => {
                          setSelectedJobTypes((prev) =>
                            prev.includes(type.value)
                              ? prev.filter((t) => t !== type.value)
                              : [...prev, type.value]
                          )
                        }}
                      >
                        <div
                          className={`mr-2 flex h-4 w-4 items-center justify-center rounded-sm border border-primary ${
                            selectedJobTypes.includes(type.value)
                              ? "bg-primary text-primary-foreground"
                              : "opacity-50 [&_svg]:invisible"
                          }`}
                        >
                          <Check className={`h-4 w-4`} />
                        </div>
                        {type.label}
                      </CommandItem>
                    ))}
                  </CommandGroup>
                </CommandList>
              </Command>
            </PopoverContent>
          </Popover>
          {/* Job Skillset */}
          <Popover>
            <PopoverTrigger asChild>
              <Button variant="outline" className="w-[250px] justify-start">
                {selectedTags.length > 0
                  ? `${getLabelsByValue(tags,selectedTags)}`
                  : "Skills"}
              </Button>
            </PopoverTrigger>
            <PopoverContent className="w-[200px] p-0">
              <Command>
                <CommandInput placeholder="Search tag..." />
                <CommandList>
                  <CommandEmpty>No tags found.</CommandEmpty>
                  <CommandGroup>
                    {tags.map((tag) => (
                      <CommandItem
                        key={tag.value}
                        onSelect={() => {
                          setSelectedTags((prev) =>
                            prev.includes(tag.value)
                              ? prev.filter((t) => t !== tag.value)
                              : [...prev, tag.value]
                          )
                        }}
                      >
                        <div
                          className={`mr-2 flex h-4 w-4 items-center justify-center rounded-sm border border-primary ${
                            selectedTags.includes(tag.value)
                              ? "bg-primary text-primary-foreground"
                              : "opacity-50 [&_svg]:invisible"
                          }`}
                        >
                          <Check className={`h-4 w-4`} />
                        </div>
                        {tag.label}
                      </CommandItem>
                    ))}
                  </CommandGroup>
                </CommandList>
              </Command>
            </PopoverContent>
          </Popover>
        </div>
        <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
          <div className="md:col-span-1">
            <ScrollArea className="h-[calc(100vh-300px)]">
              {jobs.map((job) => (
                <Card
                  key={job.id}
                  className={`mb-4 cursor-pointer ${selectedJob.id === job.id ? "border-primary" : ""}`}
                  onClick={() => setSelectedJob(job)}   
                >
                  <CardContent className="flex items-start space-x-4 p-4">
                    <Avatar>
                      <AvatarImage src={job.image} alt={job.company} />
                      <AvatarFallback>{job.company[0]}</AvatarFallback>
                    </Avatar>
                    <div>
                      <h3 className="font-semibold">{job.title}</h3>
                      <p className="text-sm text-gray-500">{job.company}</p>
                      <p className="text-sm text-gray-500">{job.location}</p>
                      <p className="text-sm text-gray-500">{job.postedAgo}</p>
                    </div>
                  </CardContent>
                </Card>
              ))}
            </ScrollArea>
          </div>
          <div className="md:col-span-2">
            {selectedJob && (
              <Card>
                <CardContent className="p-6">
                  <div className="mb-4 flex items-center space-x-4">
                    <Avatar>
                      <AvatarImage src={selectedJob.image} alt={selectedJob.company} />
                      <AvatarFallback>{selectedJob.company[0]}</AvatarFallback>
                    </Avatar>
                    <div>
                      <h2 className="text-2xl font-bold">{selectedJob.title}</h2>
                      <p className="text-gray-500">{selectedJob.company}</p>
                    </div>
                  </div>
                  <p className="mb-4 text-gray-500">{selectedJob.location} • {selectedJob.postedAgo}</p>
                  <p className="mb-2 text-lg font-semibold">Skills</p>
                  <div className="mb-4">
                    {selectedJob.skills.map((skill) => (
                      <Badge key={skill} className="mr-2 mb-2">
                        {skill}
                      </Badge>
                    ))}
                  </div>
                  <p className="mb-2 text-lg font-semibold">Description</p>
                  <p className="mb-6">{selectedJob.description}</p>
                  <Button>Apply Now</Button>
                </CardContent>
              </Card>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}   