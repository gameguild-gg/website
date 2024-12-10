'use client'

import { useState, useEffect } from 'react'
import { useForm, useFieldArray, Controller } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Checkbox } from "@/components/ui/checkbox"
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group"
import { ChevronDown, ChevronRight } from 'lucide-react'

interface ContentLink {
  order: string;
  link: string;
}

interface QuestionOption {
  text: string;
  isCorrect: boolean;
}

interface Question {
  order: number;
  type: 'text' | 'multiple-choice';
  question: string;
  answer?: string;
  options?: QuestionOption[];
  score: number;
}

interface Exercise {
  id?: string;
  moduleId?: string;
  lessonId?: string;
  order: string;
  title: string;
  description: string;
  contentLinks: ContentLink[];
  questions: Question[];
}

interface Module {
  id: string;
  order: string;
  title: string;
}

interface Lesson {
  id: string;
  title: string;
  order: string;
  moduleId: string;
}

export default function ExercisesTab({ exercises, modules, lessons, updateData }) {
  const { register, control, handleSubmit, reset, watch } = useForm<Exercise>({
    defaultValues: {
      moduleId: '',
      lessonId: '',
      order: '',
      title: '',
      description: '',
      contentLinks: [{ order: '', link: '' }],
      questions: [{ order: 1, type: 'text', question: '', answer: '', score: 1 }]
    }
  })
  const { fields: contentLinkFields, append: appendContentLink, remove: removeContentLink } = useFieldArray({
    control,
    name: "contentLinks"
  })
  const { fields: questionFields, append: appendQuestion, remove: removeQuestion } = useFieldArray({
    control,
    name: "questions"
  })
  const [editingIndex, setEditingIndex] = useState(-1)
  const [expandedModules, setExpandedModules] = useState<string[]>([])
  const [expandedLessons, setExpandedLessons] = useState<string[]>([])
  const [expandedUnassigned, setExpandedUnassigned] = useState(false)

  const watchQuestionTypes = watch("questions");
  const watchModuleId = watch("moduleId");

  useEffect(() => {
    if (watchModuleId === 'none') {
      reset((formValues) => ({
        ...formValues,
        lessonId: 'none'
      }));
    } else {
      // Reset lessonId if the selected lesson is not in the current module
      const currentLesson = lessons.find(lesson => lesson.id === watch('lessonId'));
      if (currentLesson && currentLesson.moduleId !== watchModuleId) {
        reset((formValues) => ({
          ...formValues,
          lessonId: 'none'
        }));
      }
    }
  }, [watchModuleId, reset, lessons, watch]);

  const onSubmit = (data: Exercise) => {
    const submissionData = {
      ...data,
      moduleId: data.moduleId === 'none' ? '' : data.moduleId,
      lessonId: data.lessonId === 'none' ? '' : data.lessonId,
    };

    if (editingIndex === -1) {
      updateData([...exercises, submissionData])
    } else {
      const updatedExercises = [...exercises]
      updatedExercises[editingIndex] = submissionData
      updateData(updatedExercises)
      setEditingIndex(-1)
    }
    reset()
  }

  const handleEdit = (index: number) => {
    setEditingIndex(index)
    reset(exercises[index])
  }

  const handleRemove = (index: number) => {
    const updatedExercises = exercises.filter((_, i) => i !== index)
    updateData(updatedExercises)
  }

  const toggleModule = (moduleTitle: string) => {
    setExpandedModules(prev => 
      prev.includes(moduleTitle)
        ? prev.filter(title => title !== moduleTitle)
        : [...prev, moduleTitle]
    )
  }

  const toggleLesson = (lessonTitle: string) => {
    setExpandedLessons(prev => 
      prev.includes(lessonTitle)
        ? prev.filter(title => title !== lessonTitle)
        : [...prev, lessonTitle]
    )
  }

  const toggleUnassigned = () => {
    setExpandedUnassigned(!expandedUnassigned)
  }

  const groupedExercises = exercises.reduce((acc, exercise) => {
    if (!exercise.moduleId && !exercise.lessonId) {
      if (!acc['Unassigned']) {
        acc['Unassigned'] = { 'Unassigned': [] };
      }
      acc['Unassigned']['Unassigned'].push(exercise);
    } else {
      const moduleId = exercise.moduleId || 'Unassigned';
      const lessonId = exercise.lessonId || 'Unassigned';
      if (!acc[moduleId]) {
        acc[moduleId] = {};
      }
      if (!acc[moduleId][lessonId]) {
        acc[moduleId][lessonId] = [];
      }
      acc[moduleId][lessonId].push(exercise);
    }
    return acc;
  }, {} as Record<string, Record<string, Exercise[]>>);

  const sortedModules = modules ? [...modules].sort((a, b) => Number(a.order) - Number(b.order)) : [];

  return (
    <div className="grid md:grid-cols-2 gap-4">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <Label htmlFor="module">Module (Optional)</Label>
          <Controller
            name="moduleId"
            control={control}
            render={({ field }) => (
              <Select onValueChange={field.onChange} value={field.value || undefined}>
                <SelectTrigger>
                  <SelectValue placeholder="Select module" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">None</SelectItem>
                  {modules && modules.map((module) => (
                    <SelectItem key={module.id} value={module.id}>
                      {module.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
        </div>
        <div>
          <Label htmlFor="lesson">Lesson (Optional)</Label>
          <Controller
            name="lessonId"
            control={control}
            render={({ field }) => (
              <Select onValueChange={field.onChange} value={field.value || undefined}>
                <SelectTrigger>
                  <SelectValue placeholder="Select lesson" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">None</SelectItem>
                  {lessons
                    .filter(lesson => watchModuleId === 'none' || lesson.moduleId === watchModuleId)
                    .map((lesson) => (
                      <SelectItem key={lesson.id} value={lesson.id}>
                        {lesson.title || `Untitled Lesson ${lesson.order}`}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            )}
          />
        </div>
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
          <Label>Content Links</Label>
          {contentLinkFields.map((field, index) => (
            <div key={field.id} className="flex items-center space-x-2 mt-2">
              <Input
                {...register(`contentLinks.${index}.order` as const)}
                placeholder="Order"
                type="number"
              />
              <Input
                {...register(`contentLinks.${index}.link` as const)}
                placeholder="Link"
              />
              <Button type="button" onClick={() => removeContentLink(index)} variant="destructive" size="sm">
                Remove
              </Button>
            </div>
          ))}
          <Button
            type="button"
            onClick={() => appendContentLink({ order: '', link: '' })}
            variant="outline"
            size="sm"
            className="mt-2"
          >
            Add Content Link
          </Button>
        </div>
        <div>
          <Label>Questions</Label>
          {questionFields.map((field, index) => (
            <div key={field.id} className="space-y-2 border p-4 rounded-md mt-4">
              <div className="flex items-center space-x-2">
                <Label>Question Type</Label>
                <Controller
                  name={`questions.${index}.type` as const}
                  control={control}
                  render={({ field }) => (
                    <RadioGroup
                      onValueChange={field.onChange}
                      value={field.value}
                      className="flex space-x-4"
                    >
                      <div className="flex items-center space-x-2">
                        <RadioGroupItem value="text" id={`text-${index}`} />
                        <Label htmlFor={`text-${index}`}>Text</Label>
                      </div>
                      <div className="flex items-center space-x-2">
                        <RadioGroupItem value="multiple-choice" id={`multiple-choice-${index}`} />
                        <Label htmlFor={`multiple-choice-${index}`}>Multiple Choice</Label>
                      </div>
                    </RadioGroup>
                  )}
                />
              </div>
              <div className="flex space-x-2">
                <div className="w-24">
                  <Label>Order</Label>
                  <Input 
                    type="number" 
                    {...register(`questions.${index}.order` as const, { valueAsNumber: true })} 
                    placeholder="Order" 
                  />
                </div>
                <div className="flex-grow">
                  <Label>Question</Label>
                  <Input {...register(`questions.${index}.question` as const)} placeholder="Question" />
                </div>
                <div className="w-24">
                  <Label>Score</Label>
                  <Input 
                    type="number" 
                    {...register(`questions.${index}.score` as const, { valueAsNumber: true })} 
                    placeholder="Score" 
                  />
                </div>
              </div>
              {watchQuestionTypes[index]?.type === 'text' && (
                <div>
                  <Label>Answer</Label>
                  <Input {...register(`questions.${index}.answer` as const)} placeholder="Answer" />
                </div>
              )}
              {watchQuestionTypes[index]?.type === 'multiple-choice' && (
                <div>
                  <Label>Options</Label>
                  <Controller
                    name={`questions.${index}.options` as const}
                    control={control}
                    defaultValue={[]}
                    render={({ field }) => (
                      <>
                        {field.value?.map((option, optionIndex) => (
                          <div key={optionIndex} className="flex items-center space-x-2 mt-2">
                            <Input
                              value={option.text}
                              onChange={(e) => {
                                const newOptions = [...field.value];
                                newOptions[optionIndex].text = e.target.value;
                                field.onChange(newOptions);
                              }}
                              placeholder={`Option ${optionIndex + 1}`}
                            />
                            <Checkbox
                              checked={option.isCorrect}
                              onCheckedChange={(checked) => {
                                const newOptions = [...field.value];
                                newOptions[optionIndex].isCorrect = checked as boolean;
                                field.onChange(newOptions);
                              }}
                            />
                            <Button
                              type="button"
                              onClick={() => {
                                const newOptions = field.value.filter((_, i) => i !== optionIndex);
                                field.onChange(newOptions);
                              }}
                              variant="destructive"
                              size="sm"
                            >
                              Remove
                            </Button>
                          </div>
                        ))}
                        <Button
                          type="button"
                          onClick={() => {
                            field.onChange([...field.value, { text: '', isCorrect: false }]);
                          }}
                          variant="outline"
                          size="sm"
                          className="mt-2"
                        >
                          Add Option
                        </Button>
                      </>
                    )}
                  />
                </div>
              )}
              <Button type="button" onClick={() => removeQuestion(index)} variant="destructive" size="sm">
                Remove Question
              </Button>
            </div>
          ))}
          <Button
            type="button"
            onClick={() => appendQuestion({ order: questionFields.length + 1, type: 'text', question: '', answer: '', score: 1 })}
            variant="outline"
            className="mt-4"
          >
            Add Question
          </Button>
        </div>
        <Button type="submit">Save Exercise</Button>
      </form>
      <div>
        <h3 className="text-lg font-semibold mb-2">Saved Exercises</h3>
        <ul className="space-y-2">
          {groupedExercises['Unassigned'] && (
            <li className="bg-gray-100 p-4 rounded-md">
              <div 
                className="flex items-center justify-between cursor-pointer"
                onClick={toggleUnassigned}
              >
                <span className="font-semibold">
                  {expandedUnassigned ? <ChevronDown className="inline mr-2" /> : <ChevronRight className="inline mr-2" />}
                  Unassigned Exercises
                </span>
                <span>{groupedExercises['Unassigned']['Unassigned'].length} exercise(s)</span>
              </div>
              {expandedUnassigned && (
                <ul className="mt-2 space-y-2">
                  {groupedExercises['Unassigned']['Unassigned']
                    .sort((a, b) => Number(a.order) - Number(b.order))
                    .map((exercise, exerciseIndex) => (
                      <li key={exercise.id || exerciseIndex} className="bg-white p-3 rounded-md">
                        <div className="flex justify-between items-center mb-2">
                          <span className="font-semibold">Exercise {exercise.order}: {exercise.title}</span>
                          <div className="space-x-2">
                            <Button onClick={() => handleEdit(exercises.findIndex(e => e === exercise))} variant="outline" size="sm">Edit</Button>
                            <Button onClick={() => handleRemove(exercises.findIndex(e => e === exercise))} variant="destructive" size="sm">Remove</Button>
                          </div>
                        </div>
                        <p className="text-sm text-gray-600 mb-2">{exercise.description}</p>
                        <div className="text-sm">
                          <strong>Content Links:</strong>
                          <ul className="list-disc pl-5">
                            {exercise.contentLinks && exercise.contentLinks
                              .sort((a, b) => Number(a.order) - Number(b.order))
                              .map((item, linkIndex) => (
                                <li key={linkIndex}>
                                  Order: {item.order}, Link: <a href={item.link} target="_blank" rel="noopener noreferrer" className="text-blue-500 hover:underline">{item.link}</a>
                                </li>
                              ))}
                          </ul>
                          <strong>Questions:</strong>
                          <ul className="list-disc pl-5">
                            {exercise.questions && exercise.questions
                              .sort((a, b) => a.order - b.order)
                              .map((q, qIndex) => (
                                <li key={qIndex}>
                                  <p><strong>Order:</strong> {q.order}</p>
                                  <p><strong>Q:</strong> {q.question} (Score: {q.score})</p>
                                  {q.type === 'text' && <p><strong>A:</strong> {q.answer}</p>}
                                  {q.type === 'multiple-choice' && (
                                    <ul className="list-circle pl-5">
                                      {q.options && q.options.map((option, optionIndex) => (
                                        <li key={optionIndex} className={option.isCorrect ? 'font-bold' : ''}>
                                          {option.text} {option.isCorrect && '(Correct)'}
                                        </li>
                                      ))}
                                    </ul>
                                  )}
                                </li>
                              ))}
                          </ul>
                        </div>
                      </li>
                    ))}
                </ul>
              )}
            </li>
          )}
          {sortedModules.map((module) => {
            const moduleExercises = groupedExercises[module.id] || {};
            if (Object.keys(moduleExercises).length === 0) return null;
            
            return (
              <li key={module.id} className="bg-gray-100 p-4 rounded-md">
                <div 
                  className="flex items-center justify-between cursor-pointer"
                  onClick={() => toggleModule(module.id)}
                >
                  <span className="font-semibold">
                    {expandedModules.includes(module.id) ? <ChevronDown className="inline mr-2" /> : <ChevronRight className="inline mr-2" />}
                    Module {module.order}: {module.title}
                  </span>
                  <span>{Object.values(moduleExercises).flat().length} exercise(s)</span>
                </div>
                {expandedModules.includes(module.id) && (
                  <ul className="mt-2 space-y-2">
                    {Object.entries(moduleExercises).map(([lessonId, lessonExercises]) => {
                      const lesson = lessons.find(l => l.id === lessonId) || { title: 'Unassigned', id: 'Unassigned' };
                      return (
                        <li key={lesson.id} className="bg-white p-3 rounded-md">
                          <div 
                            className="flex items-center justify-between cursor-pointer"
                            onClick={() => toggleLesson(lesson.id)}
                          >
                            <span className="font-semibold">
                              {expandedLessons.includes(lesson.id) ? <ChevronDown className="inline mr-2" /> : <ChevronRight className="inline mr-2" />}
                              {lesson.id === 'Unassigned' ? 'Unassigned to Lesson' : `Lesson: ${lesson.title}`}
                            </span>
                            <span>{lessonExercises.length} exercise(s)</span>
                          </div>
                          {expandedLessons.includes(lesson.id) && (
                            <ul className="mt-2 space-y-2">
                              {lessonExercises
                                .sort((a, b) => Number(a.order) - Number(b.order))
                                .map((exercise, exerciseIndex) => (
                                  <li key={exercise.id || exerciseIndex} className="bg-gray-50 p-3 rounded-md">
                                    <div className="flex justify-between items-center mb-2">
                                      <span className="font-semibold">Exercise {exercise.order}: {exercise.title}</span>
                                      <div className="space-x-2">
                                        <Button onClick={() => handleEdit(exercises.findIndex(e => e === exercise))} variant="outline" size="sm">Edit</Button>
                                        <Button onClick={() => handleRemove(exercises.findIndex(e => e === exercise))} variant="destructive" size="sm">Remove</Button>
                                      </div>
                                    </div>
                                    <p className="text-sm text-gray-600 mb-2">{exercise.description}</p>
                                    <div className="text-sm">
                                      <strong>Content Links:</strong>
                                      <ul className="list-disc pl-5">
                                        {exercise.contentLinks && exercise.contentLinks
                                          .sort((a, b) => Number(a.order) - Number(b.order))
                                          .map((item, linkIndex) => (
                                            <li key={linkIndex}>
                                              Order: {item.order}, Link: <a href={item.link} target="_blank" rel="noopener noreferrer" className="text-blue-500 hover:underline">{item.link}</a>
                                            </li>
                                          ))}
                                      </ul>
                                      <strong>Questions:</strong>
                                      <ul className="list-disc pl-5">
                                        {exercise.questions && exercise.questions
                                          .sort((a, b) => a.order - b.order)
                                          .map((q, qIndex) => (
                                            <li key={qIndex}>
                                              <p><strong>Order:</strong> {q.order}</p>
                                              <p><strong>Q:</strong> {q.question} (Score: {q.score})</p>
                                              {q.type === 'text' && <p><strong>A:</strong> {q.answer}</p>}
                                              {q.type === 'multiple-choice' && (
                                                <ul className="list-circle pl-5">
                                                  {q.options && q.options.map((option, optionIndex) => (
                                                    <li key={optionIndex} className={option.isCorrect ? 'font-bold' : ''}>
                                                      {option.text} {option.isCorrect && '(Correct)'}
                                                    </li>
                                                  ))}
                                                </ul>
                                              )}
                                            </li>
                                          ))}
                                      </ul>
                                    </div>
                                  </li>
                                ))}
                            </ul>
                          )}
                        </li>
                      );
                    })}
                  </ul>
                )}
              </li>
            )
          })}
        </ul>
      </div>
    </div>
  )
}

