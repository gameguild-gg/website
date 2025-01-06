'use client'

import { useState } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import SavedItemsDisplay from '../SavedItemsDisplay'

interface Module {
  id: string;
  parentId?: string;
  order: string;
  title: string;
  description: string;
  thumbnail: string;
}

export default function ModulesTab({ modules: allModules, updateData }) {
  const { register, handleSubmit, reset, control } = useForm()
  const [editingIndex, setEditingIndex] = useState(-1)

  const getNextId = () => {
    if (allModules.length === 0) return '10';
    const maxId = Math.max(...allModules.map(m => parseInt(m.id)));
    return (maxId + 1).toString();
  }

  const onSubmit = (data: Module) => {
    const submissionData = {
      ...data,
      id: editingIndex === -1 ? getNextId() : allModules[editingIndex].id,
      parentId: data.parentId === "none" ? undefined : data.parentId,
    };

    if (editingIndex === -1) {
      updateData([...allModules, submissionData])
    } else {
      const updatedModules = [...allModules]
      updatedModules[editingIndex] = submissionData
      updateData(updatedModules)
      setEditingIndex(-1)
    }
    reset()
  }

  const handleEdit = (index) => {
    setEditingIndex(index)
    reset(allModules[index])
  }

  const handleRemove = (index) => {
    const updatedModules = allModules.filter((_, i) => i !== index)
    updateData(updatedModules)
  }

  const getChildModules = (module: Module) => {
    return allModules.filter(m => m.parentId === module.id);
  }

  const renderAdditionalInfo = (module: Module) => {
    return (
      <>
        {module.parentId && (
          <p className="text-xs text-gray-500">Parent: {allModules.find(m => m.id === module.parentId)?.title || 'Unknown'}</p>
        )}
      </>
    );
  }

  const renderSpecificFields = (module: Module) => {
    return (
      <>
        {module.thumbnail && <img src={module.thumbnail} alt={`Thumbnail for ${module.title}`} className="w-20 h-20 object-cover rounded mt-2" />}
      </>
    );
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
        <div>
          <Label htmlFor="parentId">Parent Module (Optional)</Label>
          <Controller
            name="parentId"
            control={control}
            defaultValue="none"
            render={({ field }) => (
              <Select onValueChange={field.onChange} value={field.value}>
                <SelectTrigger>
                  <SelectValue placeholder="Select parent module" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">None</SelectItem>
                  {allModules.map((module) => (
                    <SelectItem key={module.id} value={module.id}>
                      {module.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
        </div>
        <Button type="submit">Save Module</Button>
      </form>
      <SavedItemsDisplay
        items={allModules}
        itemType="module"
        onEdit={handleEdit}
        onRemove={handleRemove}
        getChildItems={getChildModules}
        renderAdditionalInfo={renderAdditionalInfo}
        renderSpecificFields={renderSpecificFields}
      />
    </div>
  )
}

