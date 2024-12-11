'use client'

import { useState, useEffect } from 'react'
import { useForm, useFieldArray, Controller } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { ChevronDown, ChevronRight } from 'lucide-react'
import ShortAnswerQuestion from './question-types/ShortAnswerQuestion'
import LongAnswerQuestion from './question-types/LongAnswerQuestion'
import EssayQuestion from './question-types/EssayQuestion'
import MultipleAnswersQuestion from './question-types/MultipleAnswersQuestion'
import NumericalAnswersQuestion from './question-types/NumericalAnswersQuestion'
import CompleteSentenceQuestion from './question-types/CompleteSentenceQuestion'
import MultipleChoiceQuestion from './question-types/MultipleChoiceQuestion'
import TrueFalseQuestion from './question-types/TrueFalseQuestion'
import MatchingQuestion from './question-types/MatchingQuestion'
import CodeValidationQuestion from './question-types/CodeValidationQuestion'
import FileAnswerQuestion from './question-types/FileAnswerQuestion'
import ContentLinks from '../ContentLinks'

interface ContentLink {
  order: string;
  link: string;
}

interface Question {
  order: number;
  type: string;
  question: string;
  correctScore: number;
  incorrectScore: number;
  time: number;
  contentLinks: ContentLink[];
  // Additional fields will be added dynamically based on the question type
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
  time: {
    hours: number;
    minutes: number;
  };
  averageGrades: 'no average' | 'lesson average' | 'module average' | 'course average';
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
  const { register, control, handleSubmit, reset, watch, setValue } = useForm<Exercise>({
    defaultValues: {
      moduleId: '',
      lessonId: '',
      order: '',
      title: '',
      description: '',
      contentLinks: [], 
      questions: [{
        order: 1,
        type: 'short answer',
        question: '',
        correctScore: 0,
        incorrectScore: 0,
        time: 0,
        contentLinks: []
      }],
      time: { hours: 0, minutes: 0 },
      averageGrades: 'no average'
    }
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
  const watchLessonId = watch("lessonId");

  useEffect(() => {
    if (watchModuleId === 'none') {
      reset((formValues) => ({
        ...formValues,
        lessonId: 'none',
        averageGrades: 'no average'
      }));
    } else {
      const currentLesson = lessons.find(lesson => lesson.id === watchLessonId);
      if (currentLesson && currentLesson.moduleId !== watchModuleId) {
        reset((formValues) => ({
          ...formValues,
          lessonId: 'none',
          averageGrades: 'module average'
        }));
      } else {
        setValue('averageGrades', 'module average');
      }
    }
  }, [watchModuleId, reset, lessons, watchLessonId, setValue]);

  useEffect(() => {
    if (watchLessonId && watchLessonId !== 'none') {
      setValue('averageGrades', 'lesson average');
    }
  }, [watchLessonId, setValue]);

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

  const renderQuestionFields = (questionIndex: number) => {
    const questionType = watchQuestionTypes[questionIndex]?.type;
    switch (questionType) {
      case 'short answer':
        return <ShortAnswerQuestion control={control} index={questionIndex} />;
      case 'long answer':
        return <LongAnswerQuestion control={control} index={questionIndex} />;
      case 'essay':
        return <EssayQuestion control={control} index={questionIndex} />;
      case 'multiple answers':
        return <MultipleAnswersQuestion control={control} index={questionIndex} />;
      case 'numerical answers':
        return <NumericalAnswersQuestion control={control} index={questionIndex} />;
      case 'complete sentence':
        return <CompleteSentenceQuestion control={control} index={questionIndex} />;
      case 'multiple choice':
        return <MultipleChoiceQuestion control={control} index={questionIndex} />;
      case 'true/false':
        return <TrueFalseQuestion control={control} index={questionIndex} />;
      case 'matching':
        return <MatchingQuestion control={control} index={questionIndex} />;
      case 'code validation':
        return <CodeValidationQuestion control={control} index={questionIndex} />;
      case 'file answer':
        return <FileAnswerQuestion control={control} index={questionIndex} />;
      default:
        return null;
    }
  };

  return (
    <div className="grid md:grid-cols-2 gap-4">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
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
        <div className="grid grid-cols-3 gap-4">
          <div>
            <Label>Time (Hours)</Label>
            <Input
              type="number"
              {...register('time.hours', { valueAsNumber: true })}
              placeholder="Hours"
            />
          </div>
          <div>
            <Label>Time (Minutes)</Label>
            <Input
              type="number"
              {...register('time.minutes', { valueAsNumber: true })}
              placeholder="Minutes"
            />
          </div>
          <div>
            <Label htmlFor="averageGrades">Average Grades</Label>
            <Controller
              name="averageGrades"
              control={control}
              render={({ field }) => (
                <Select onValueChange={field.onChange} value={field.value}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select average type" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="no average">No Average</SelectItem>
                    <SelectItem value="lesson average">Lesson Average</SelectItem>
                    <SelectItem value="module average">Module Average</SelectItem>
                    <SelectItem value="course average">Course Average</SelectItem>
                  </SelectContent>
                </Select>
              )}
            />
          </div>
        </div>
        <div>
          <Label>Content Links</Label>
          <ContentLinks control={control} name="contentLinks" />
        </div>
        <div>
          <Label>Questions</Label>
          {questionFields.map((field, index) => (
            <div key={field.id} className="space-y-4 border p-4 rounded-md mt-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label>Order</Label>
                  <Input 
                    type="number" 
                    {...register(`questions.${index}.order` as const, { valueAsNumber: true })} 
                    placeholder="Order" 
                  />
                </div>
                <div>
                  <Label>Question Type</Label>
                  <Controller
                    name={`questions.${index}.type` as const}
                    control={control}
                    render={({ field }) => (
                      <Select onValueChange={field.onChange} value={field.value}>
                        <SelectTrigger>
                          <SelectValue placeholder="Select question type" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="short answer">Short Answer</SelectItem>
                          <SelectItem value="long answer">Long Answer</SelectItem>
                          <SelectItem value="essay">Essay</SelectItem>
                          <SelectItem value="multiple answers">Multiple Answers</SelectItem>
                          <SelectItem value="numerical answers">Numerical Answers</SelectItem>
                          <SelectItem value="complete sentence">Complete Sentence</SelectItem>
                          <SelectItem value="multiple choice">Multiple Choice</SelectItem>
                          <SelectItem value="true/false">True/False</SelectItem>
                          <SelectItem value="matching">Matching</SelectItem>
                          <SelectItem value="code validation">Code Validation</SelectItem>
                          <SelectItem value="file answer">File Answer</SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
              </div>
              <div>
                <Label>Question</Label>
                <Textarea {...register(`questions.${index}.question` as const)} placeholder="Question" />
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div>
                  <Label>Correct Score</Label>
                  <Input 
                    type="number" 
                    {...register(`questions.${index}.correctScore` as const, { valueAsNumber: true })} 
                    placeholder="Correct Score" 
                  />
                </div>
                <div>
                  <Label>Incorrect Score</Label>
                  <Input 
                    type="number" 
                    {...register(`questions.${index}.incorrectScore` as const, { valueAsNumber: true })} 
                    placeholder="Incorrect Score" 
                  />
                </div>
                <div>
                  <Label>Time (minutes)</Label>
                  <Input 
                    type="number" 
                    {...register(`questions.${index}.time` as const, { valueAsNumber: true })} 
                    placeholder="Time" 
                  />
                </div>
              </div>
              <div>
                <Label>Content Links</Label>
                <Controller
                  name={`questions.${index}.contentLinks` as const}
                  control={control}
                  defaultValue={[]}
                  render={({ field }) => (
                    <>
                      {field.value.map((link, linkIndex) => (
                        <div key={linkIndex} className="flex items-center space-x-2 mt-2">
                          <Input
                            value={link.order}
                            onChange={(e) => {
                              const newLinks = [...field.value];
                              newLinks[linkIndex].order = e.target.value;
                              field.onChange(newLinks);
                            }}
                            placeholder="Order"
                            type="number"
                            className="w-20"
                          />
                          <Input
                            value={link.link}
                            onChange={(e) => {
                              const newLinks = [...field.value];
                              newLinks[linkIndex].link = e.target.value;
                              field.onChange(newLinks);
                            }}
                            placeholder="Link"
                            className="flex-grow"
                          />
                          <Button
                            type="button"
                            onClick={() => {
                              const newLinks = field.value.filter((_, i) => i !== linkIndex);
                              field.onChange(newLinks);
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
                          field.onChange([...field.value, { order: '', link: '' }]);
                        }}
                        variant="outline"
                        size="sm"
                        className="mt-2"
                      >
                        Add Content Link
                      </Button>
                    </>
                  )}
                />
              </div>
              {renderQuestionFields(index)}
              <Button type="button" onClick={() => removeQuestion(index)} variant="destructive" size="sm">
                Remove Question
              </Button>
            </div>
          ))}
          <Button
            type="button"
            onClick={() => appendQuestion({ 
              order: questionFields.length + 1, 
              type: 'short answer', 
              question: '', 
              correctScore: 0,
              incorrectScore: 0,
              time: 0,
              contentLinks: []
            })}
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
                          <p><strong>Time:</strong> {exercise.time.hours}h {exercise.time.minutes}m</p>
                          <p><strong>Average Grades:</strong> {exercise.averageGrades}</p>
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
                                  <p><strong>Type:</strong> {q.type}</p>
                                  <p><strong>Q:</strong> {q.question}</p>
                                  <p><strong>Correct Score:</strong> {q.correctScore}</p>
                                  <p><strong>Incorrect Score:</strong> {q.incorrectScore}</p>
                                  <p><strong>Time:</strong> {q.time} minutes</p>
                                  {/* Render question-specific fields here */}
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
                                      <p><strong>Time:</strong> {exercise.time.hours}h {exercise.time.minutes}m</p>
                                      <p><strong>Average Grades:</strong> {exercise.averageGrades}</p>
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
                                              <p><strong>Type:</strong> {q.type}</p>
                                              <p><strong>Q:</strong> {q.question}</p>
                                              <p><strong>Correct Score:</strong> {q.correctScore}</p>
                                              <p><strong>Incorrect Score:</strong> {q.incorrectScore}</p>
                                              <p><strong>Time:</strong> {q.time} minutes</p>
                                              {/* Render question-specific fields here */}
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

