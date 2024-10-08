"use client"

import { useState } from "react"
import { Api, JobsApi } from '@game-guild/apiclient';
import { getSession } from 'next-auth/react';
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import { Checkbox } from "@/components/ui/checkbox"

const skills = [
  { id: "react", label: "React" },
  { id: "typescript", label: "TypeScript" },
  { id: "nodejs", label: "Node.js" },
  { id: "python", label: "Python" },
  { id: "java", label: "Java" },
  { id: "csharp", label: "C#" },
  { id: "ruby", label: "Ruby" },
  { id: "php", label: "PHP" },
  { id: "go", label: "Go" },
  { id: "rust", label: "Rust" },
]

export default function JobPost() {
  const [title, setTitle] = useState("")
  const [description, setDescription] = useState("")
  const [location, setLocation] = useState("")
  const [selectedSkills, setSelectedSkills] = useState<string[]>([])

  const api = new JobsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  const handleSkillSelection = (skill: string) => {
    setSelectedSkills((prev) =>
      prev.includes(skill)
        ? prev.filter((s) => s !== skill)
        : [...prev, skill]
    )
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    // Handle form submission here
    console.log({ title, description, location, selectedSkills })
    //
    postJob();
  }

  const postJob = async ():Promise<void> => {
    const session = await getSession();
    if (!session) {
      window.location.href = '/connect';
      return;
    }
    
    const response = await api.createOneBaseJobControllerJobPostEntity(
      {
        title: title,
        slug: title,
        summary: description,
        body: description,
      } as Api.JobPostCreateDto,
      {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      }
    );
    console.log("/jobs API RESPONSE:\n",response)
  }

  return (
    <div className="min-h-screen bg-gray-100 p-4">
      <Card className="mx-auto max-w-2xl">
        <CardContent className="p-6">
          <h1 className="mb-6 text-2xl font-bold">Post a New Job</h1>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="title">Job Title</Label>
              <Input
                id="title"
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="description">Job Description</Label>
              <Textarea
                id="description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                required
                className="min-h-[100px]"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="location">Location</Label>
              <Input
                id="location"
                value={location}
                onChange={(e) => setLocation(e.target.value)}
                required
              />
            </div>
            <div className="space-y-2">
              <Label>Skills</Label>
              <div className="grid grid-cols-2 gap-2">
                {skills.map((skill) => (
                  <div key={skill.id} className="flex items-center space-x-2">
                    <Checkbox
                      id={skill.id}
                      checked={selectedSkills.includes(skill.id)}
                      onCheckedChange={() => handleSkillSelection(skill.id)}
                    />
                    <Label htmlFor={skill.id}>{skill.label}</Label>
                  </div>
                ))}
              </div>
            </div>
            <div className="flex justify-between pt-4">
              <Button type="button" variant="outline">
                Return
              </Button>
              <Button type="submit" onClick={handleSubmit}>Post Job</Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}