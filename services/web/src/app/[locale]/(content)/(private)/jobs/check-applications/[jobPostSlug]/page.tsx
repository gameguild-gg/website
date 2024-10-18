'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import Image from 'next/image'
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'

// Define the structure for an applicant
interface Applicant {
  id: number
  name: string
  image: string
  stage: number
  rejected: boolean
}

// Define the stages
const stages = [
  'Applied',
  'Application Accepted',
  'Tests and Interviews',
  'Hiring Proposal Sent',
  'Hiring Process Complete'
]

export default function JobApplicantManagement({params}) {
  // Sample job data
  const job = {
    title: "Senior Frontend Developer",
    tags: ["React", "TypeScript", "Next.js"],
    description: "We are seeking an experienced frontend developer to join our team and help build cutting-edge web applications."
  }

  // Sample applicant data
  const [applicants, setApplicants] = useState<Applicant[]>([
    { id: 1, name: "Alice Johnson", image: "/assets/images/placeholder.svg?height=40&width=40", stage: 0, rejected: false },
    { id: 2, name: "Bob Smith", image: "/assets/images/placeholder.svg?height=40&width=40", stage: 1, rejected: false },
    { id: 3, name: "Charlie Brown", image: "/assets/images/placeholder.svg?height=40&width=40", stage: 2, rejected: false },
    { id: 4, name: "Diana Prince", image: "/assets/images/placeholder.svg?height=40&width=40", stage: 3, rejected: false },
    { id: 5, name: "Ethan Hunt", image: "/assets/images/placeholder.svg?height=40&width=40", stage: 4, rejected: false },
  ])

  const [dialogOpen, setDialogOpen] = useState(false)
  const [currentAction, setCurrentAction] = useState<{ type: 'advance' | 'reject', applicantId: number } | null>(null)
  
  const router = useRouter()
  const { jobPostSlug } = params
  console.log("SLUG:\n",jobPostSlug)

  const handleCheckProfile = () => {
    // Open new tab to check user profile
    // BASE_URL+profile/user-slug?
  }

  const handleAction = (type: 'advance' | 'reject', applicantId: number) => {
    setCurrentAction({ type, applicantId })
    setDialogOpen(true)
  }

  const confirmAction = () => {
    if (currentAction) {
      setApplicants(prevApplicants =>
        prevApplicants.map(applicant => {
          if (applicant.id === currentAction.applicantId) {
            if (currentAction.type === 'advance') {
              return { ...applicant, stage: Math.min(applicant.stage + 1, stages.length - 1) }
            } else {
              return { ...applicant, rejected: true }
            }
          }
          return applicant
        })
      )
    }
    setDialogOpen(false)
  }

  return (
    <div className="container mx-auto p-4">
      <h1 className="text-3xl font-bold mb-6">Job Applicant Management</h1>

      <Card className="mb-8">
        <CardHeader>
          <CardTitle>{job.title}</CardTitle>
          <CardDescription>
            {job.tags.map((tag, index) => (
              <Badge key={index} variant="secondary" className="mr-2">
                {tag}
              </Badge>
            ))}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <p>{job.description}</p>
        </CardContent>
      </Card>

      {stages.map((stage, stageIndex) => (
        <div key={stageIndex} className="mb-8">
          <h2 className="text-2xl font-semibold mb-4">{stage}</h2>
          {applicants
            .filter(applicant => applicant.stage === stageIndex)
            .map(applicant => (
              <Card key={applicant.id} className={`mb-4 ${applicant.rejected ? 'opacity-50' : ''}`}>
                <CardContent className="flex items-center justify-between p-4">
                  <div className="flex items-center">
                    <Avatar>
                      <AvatarImage src={applicant.image} alt={applicant.name} />
                      <AvatarFallback>{applicant.name[0]}</AvatarFallback>
                    </Avatar>
                    <span className="ml-3 font-medium">{applicant.name}</span>
                  </div>
                  <div className="space-x-2">
                    <Button variant="outline">Check Profile</Button>
                    <Button
                      onClick={() => handleAction('advance', applicant.id)}
                      disabled={applicant.rejected || applicant.stage === stages.length - 1}
                    >
                      Advance Process
                    </Button>
                    <Button
                      variant="destructive"
                      onClick={() => handleAction('reject', applicant.id)}
                      disabled={applicant.rejected}
                    >
                      Reject
                    </Button>
                  </div>
                </CardContent>
              </Card>
            ))}
        </div>
      ))}

      <AlertDialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {currentAction?.type === 'advance' ? 'Advance Applicant' : 'Reject Applicant'}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {currentAction?.type === 'advance'
                ? 'Are you sure you want to advance this applicant to the next stage?'
                : 'Are you sure you want to reject this applicant?'}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={confirmAction}>Confirm</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}