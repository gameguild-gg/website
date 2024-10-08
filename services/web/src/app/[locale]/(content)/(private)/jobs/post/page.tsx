"use client"

import { useEffect, useState } from "react"
import { Api, JobPostsApi, JobTagsApi } from '@game-guild/apiclient';
import { getSession } from 'next-auth/react';
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import { Checkbox } from "@/components/ui/checkbox"


export default function JobPost() {
  const [jobTags, setJobTags] = useState<any>([])
  const [title, setTitle] = useState("")
  const [description, setDescription] = useState("")
  const [location, setLocation] = useState("")
  const [selectedSkills, setSelectedSkills] = useState<Api.JobTagEntity[]>([])

  const jobPostsApi = new JobPostsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });
  const jobTagsApi = new JobTagsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  useEffect(() => {
    loadJobTags()
  },[]);

  const loadJobTags = async () =>{
    const session = await getSession();
    if (!session) {
      window.location.href = '/connect';
      return;
    }
    const response = await jobTagsApi.getManyBaseJobTagControllerJobTagEntity(
      {},
      { headers: { Authorization: `Bearer ${session.accessToken}` }, }
    )
    // console.log('GET ALL JOB TAGS RESPONSE:\n',response)
    if (response.status == 200){
      setJobTags(response.body)
    }
  }

  const handleSkillSelection = (skill: Api.JobTagEntity) => {
    setSelectedSkills((prev) =>
      prev.includes(skill)
        ? prev.filter((s) => s !== skill)
        : [...prev, skill]
    )
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    postJob();
  }

  const postJob = async ():Promise<void> => {
    const session:any = await getSession();
    if (!session) {
      window.location.href = '/connect';
      return;
    }
    
    const response = await jobPostsApi.createOneBaseJobPostControllerJobPostEntity(
      {
        title: title,
        slug: title,
        summary: description,
        body: description,
        location: location,
        tags: selectedSkills,
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
            {(jobTags && jobTags.length > 0) &&
              <div className="space-y-2">
                <Label>Skills</Label>
                <div className="grid grid-cols-2 gap-2">
                  {jobTags.map((skill:Api.JobTagEntity) => (
                    <div key={skill.id} className="flex items-center space-x-2">
                      <Checkbox
                        id={skill.id}
                        checked={selectedSkills.includes(skill)}
                        onCheckedChange={() => handleSkillSelection(skill)}
                      />
                      <Label htmlFor={skill.id}>{skill.name}</Label>
                    </div>
                  ))}
                </div>
            </div>
            }
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