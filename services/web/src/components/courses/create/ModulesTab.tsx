'use client'

import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"

export default function ModulesTab({ modules, updateData }) {
  const { register, handleSubmit, reset } = useForm()
  const [editingIndex, setEditingIndex] = useState(-1)

  const onSubmit = (data) => {
    if (editingIndex === -1) {
      updateData([...modules, data])
    } else {
      const updatedModules = [...modules]
      updatedModules[editingIndex] = data
      updateData(updatedModules)
      setEditingIndex(-1)
    }
    reset()
  }

  const handleEdit = (index) => {
    setEditingIndex(index)
    reset(modules[index])
  }

  const handleRemove = (index) => {
    const updatedModules = modules.filter((_, i) => i !== index)
    updateData(updatedModules)
  }

  return (
    <div className="grid md:grid-cols-2 gap-4">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <Label htmlFor="order">Order</Label>
          <Input id="order" type="number" {...register('order')} />
        </div>
        <div>
          <Label htmlFor="title">Title</Label>
          <Input id="title" {...register('title')} />
        </div>
        <div>
          <Label htmlFor="description">Description</Label>
          <Textarea id="description" {...register('description')} />
        </div>
        <div>
          <Label htmlFor="thumbnail">Thumbnail Image URL</Label>
          <Input id="thumbnail" {...register('thumbnail')} />
        </div>
        <Button type="submit">Save Module</Button>
      </form>
      <div>
        <h3 className="text-lg font-semibold mb-2">Saved Modules</h3>
        <ul className="space-y-2">
          {modules.map((module, index) => (
            <li key={index} className="flex justify-between items-center bg-gray-100 p-2 rounded-md">
              <div>
                <span className="font-semibold">{module.title}</span>
                <p className="text-sm text-gray-600">{module.description.slice(0, 50)}...</p>
              </div>
              <div className="flex space-x-2">
                <Button onClick={() => handleEdit(index)} variant="outline" size="sm">Edit</Button>
                <Button onClick={() => handleRemove(index)} variant="destructive" size="sm">Remove</Button>
                {module.thumbnail && <img src={module.thumbnail} alt={`Thumbnail for ${module.title}`} className="w-10 h-10 object-cover rounded" />}
              </div>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}
