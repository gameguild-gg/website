'use client'

import { useEffect } from 'react'
import { useForm, useFieldArray, Controller } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"

export default function InformationTab({ data, updateData }) {
  const { register, control, handleSubmit, reset } = useForm({
    defaultValues: {
      contentDemoLinks: [{ order: '', link: '' }]
    }
  })
  const { fields, append, remove } = useFieldArray({
    control,
    name: "contentDemoLinks"
  })

  useEffect(() => {
    reset(data)
  }, [data, reset])

  const onSubmit = (formData) => {
    updateData(formData)
  }

  return (
    <div className="grid md:grid-cols-2 gap-4">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <Label htmlFor="title">Title</Label>
          <Input id="title" {...register('title')} />
        </div>
        <div>
          <Label htmlFor="shortDescription">Brief Description</Label>
          <Textarea id="shortDescription" {...register('shortDescription')} />
        </div>
        <div>
          <Label htmlFor="fullDescription">Full Description</Label>
          <Textarea id="fullDescription" {...register('fullDescription')} className="min-h-[200px]" />
        </div>
        <div>
          <Label htmlFor="customUrl">Custom URL</Label>
          <Input id="customUrl" {...register('customUrl')} />
        </div>
        <div>
          <Label htmlFor="thumbnail">Thumbnail Image URL</Label>
          <Input id="thumbnail" {...register('thumbnail')} />
        </div>
        <div>
          <Label>Content Demo Links</Label>
          {fields.map((field, index) => (
            <div key={field.id} className="flex items-center space-x-2 mt-2">
              <Input
                {...register(`contentDemoLinks.${index}.order`)}
                placeholder="Order"
                type="number"
              />
              <Input
                {...register(`contentDemoLinks.${index}.link`)}
                placeholder="Link"
              />
              <Button type="button" onClick={() => remove(index)} variant="destructive" size="sm">
                Remove
              </Button>
            </div>
          ))}
          <Button
            type="button"
            onClick={() => append({ order: '', link: '' })}
            variant="outline"
            size="sm"
            className="mt-2"
          >
            Add New Content
          </Button>
        </div>
        <Button type="submit">Save Data</Button>
      </form>
      <div>
        <h3 className="text-lg font-semibold mb-2">Preview</h3>
        <div className="space-y-2">
          <h4 className="font-semibold">Title: {data.title}</h4>
          <p><strong>Brief Description:</strong> {data.shortDescription}</p>
          <p><strong>Full Description:</strong> {data.fullDescription}</p>
          <p><strong>Custom URL:</strong> {data.customUrl}</p>
          <div>
            <strong>Thumbnail:</strong>
            {data.thumbnail && <img src={data.thumbnail} alt="Thumbnail" className="max-w-full h-auto" />}
          </div>
          <div>
            <strong>Content Demo Links:</strong>
            <ul className="list-disc pl-5">
              {data.contentDemoLinks && data.contentDemoLinks.map((item, index) => (
                <li key={index}>
                  Order: {item.order}, Link: <a href={item.link} target="_blank" rel="noopener noreferrer">{item.link}</a>
                </li>
              ))}
            </ul>
          </div>
        </div>
      </div>
    </div>
  )
}

