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
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible"

interface ContentLink {
  order: string;
  link: string;
}

interface Question {
  id: string;
  exerciseId: string;
  order: number;
  type: string;
  question: string;
  correctScore: number | undefined;
  incorrectScore: number | undefined;
  time: number | undefined;
  contentLinks: ContentLink[];
  // Additional fields will be added dynamically based on the question type
}

interface Exercise {
  id: string;
  title: string;
}

export default function QuestionsTab({ questions, exercises, updateData }) {
  const { register, control, handleSubmit, reset, watch, setValue } = useForm<Question>({
    defaultValues: {
      exerciseId: 'unassigned',
      order: 1,
      type: 'short answer',
      question: '',
      correctScore: undefined,
      incorrectScore: undefined,
      time: undefined,
      contentLinks: []
    }
  })
  const [editingIndex, setEditingIndex] = useState(-1)
  const [expandedExercises, setExpandedExercises] = useState<string[]>([])
  const [collapsedQuestions, setCollapsedQuestions] = useState<number[]>([])
  const [selectedExerciseId, setSelectedExerciseId] = useState<string>('unassigned'); // Added state for selected exercise

  const watchQuestionTypes = watch("type")
  const watchExerciseId = watch("exerciseId")

  const getNextQuestionId = () => {
    if (questions.length === 0) return '10';
    const maxId = Math.max(...questions.map(q => parseInt(q.id)));
    return (maxId + 1).toString();
  }

  const onSubmit = (data: Question) => {
    const submissionData = {
      ...data,
      id: editingIndex === -1 ? getNextQuestionId() : questions[editingIndex].id,
      exerciseId: selectedExerciseId === 'unassigned' ? '' : selectedExerciseId,
    };

    if (editingIndex === -1) {
      updateData([...questions, submissionData])
    } else {
      const updatedQuestions = [...questions]
      updatedQuestions[editingIndex] = submissionData
      updateData(updatedQuestions)
      setEditingIndex(-1)
    }
    
    reset({
      exerciseId: selectedExerciseId === '' ? 'unassigned' : selectedExerciseId,
      order: questions.filter(q => q.exerciseId === (selectedExerciseId === 'unassigned' ? '' : selectedExerciseId)).length + 1,
      type: 'short answer',
      question: '',
      correctScore: undefined,
      incorrectScore: undefined,
      time: undefined,
      contentLinks: []
    })
  }

  const handleEdit = (index: number) => {
    setEditingIndex(index)
    reset(questions[index])
  }

  const handleRemove = (index: number) => {
    const updatedQuestions = questions.filter((_, i) => i !== index)
    updateData(updatedQuestions)
  }

  const toggleExercise = (exerciseId: string) => {
    setExpandedExercises(prev => 
      prev.includes(exerciseId)
        ? prev.filter(id => id !== exerciseId)
        : [...prev, exerciseId]
    )
  }

  const toggleQuestionCollapse = (index: number) => {
    setCollapsedQuestions(prev => 
      prev.includes(index) ? prev.filter(i => i !== index) : [...prev, index]
    );
  };

  const groupedQuestions = questions.reduce((acc, question) => {
    const exerciseId = question.exerciseId || 'Unassigned';
    if (!acc[exerciseId]) {
      acc[exerciseId] = [];
    }
    acc[exerciseId].push(question);
    return acc;
  }, {} as Record<string, Question[]>);

  const renderQuestionFields = (questionType: string) => {
    switch (questionType) {
      case 'short answer':
        return <ShortAnswerQuestion control={control} index={0} />;
      case 'long answer':
        return <LongAnswerQuestion control={control} index={0} />;
      case 'essay':
        return <EssayQuestion control={control} index={0} />;
      case 'multiple answers':
        return <MultipleAnswersQuestion control={control} index={0} />;
      case 'numerical answers':
        return <NumericalAnswersQuestion control={control} index={0} />;
      case 'complete sentence':
        return <CompleteSentenceQuestion control={control} index={0} />;
      case 'multiple choice':
        return <MultipleChoiceQuestion control={control} index={0} />;
      case 'true/false':
        return <TrueFalseQuestion control={control} index={0} />;
      case 'matching':
        return <MatchingQuestion control={control} index={0} />;
      case 'code validation':
        return <CodeValidationQuestion control={control} index={0} />;
      case 'file answer':
        return <FileAnswerQuestion control={control} index={0} />;
      default:
        return null;
    }
  };

  return (
    <div className="grid md:grid-cols-2 gap-4">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <Label htmlFor="exercise">Exercise</Label>
          <Controller
            name="exerciseId"
            control={control}
            defaultValue={selectedExerciseId} // Set default value
            render={({ field }) => (
              <Select 
                onValueChange={(value) => {
                  field.onChange(value);
                  setSelectedExerciseId(value); // Update selectedExerciseId
                }} 
                value={field.value || undefined}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select exercise" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="unassigned">Unassigned</SelectItem>
                  {exercises.map((exercise) => (
                    <SelectItem key={exercise.id} value={exercise.id}>
                      {exercise.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
        </div>
        <div>
          <Label htmlFor="order">Order</Label>
          <Input id="order" type="number" {...register('order', { valueAsNumber: true })} />
        </div>
        <div>
          <Label htmlFor="type">Question Type</Label>
          <Controller
            name="type"
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
        <div>
          <Label htmlFor="question">Question</Label>
          <Textarea id="question" {...register('question')} />
        </div>
        <div className="grid grid-cols-3 gap-4">
          <div>
            <Label htmlFor="correctScore">Correct Score</Label>
            <Input 
              id="correctScore"
              type="number" 
              step="0.01"
              {...register('correctScore', { valueAsNumber: true })} 
            />
          </div>
          <div>
            <Label htmlFor="incorrectScore">Incorrect Score</Label>
            <Input 
              id="incorrectScore"
              type="number" 
              step="0.01"
              {...register('incorrectScore', { valueAsNumber: true })} 
            />
          </div>
          <div>
            <Label htmlFor="time">Time (minutes)</Label>
            <Input 
              id="time"
              type="number" 
              step="0.01"
              {...register('time', { valueAsNumber: true })} 
            />
          </div>
        </div>
        <div>
          <Label>Content Links</Label>
          <ContentLinks control={control} name="contentLinks" />
        </div>
        {renderQuestionFields(watchQuestionTypes)}
        <Button type="submit">Save Question</Button>
      </form>
      <div>
        <h3 className="text-lg font-semibold mb-2">Saved Questions</h3>
        <ul className="space-y-2">
          {Object.entries(groupedQuestions).map(([exerciseId, exerciseQuestions]) => {
            const exercise = exercises.find(e => e.id === exerciseId) || { title: 'Unassigned', id: 'Unassigned' };
            return (
              <li key={exercise.id} className="bg-gray-100 p-4 rounded-md">
                <div 
                  className="flex items-center justify-between cursor-pointer"
                  onClick={() => toggleExercise(exercise.id)}
                >
                  <span className="font-semibold">
                    {expandedExercises.includes(exercise.id) ? <ChevronDown className="inline mr-2" /> : <ChevronRight className="inline mr-2" />}
                    {exercise.id === 'Unassigned' ? 'Unassigned Questions' : `Exercise: ${exercise.title}`}
                  </span>
                  <span>{exerciseQuestions.length} question(s)</span>
                </div>
                {expandedExercises.includes(exercise.id) && (
                  <ul className="mt-2 space-y-2">
                    {exerciseQuestions
                      .sort((a, b) => a.order - b.order)
                      .map((question, questionIndex) => (
                        <Collapsible 
                          key={question.id} 
                          open={!collapsedQuestions.includes(questionIndex)}
                          onOpenChange={() => toggleQuestionCollapse(questionIndex)}
                          className="bg-white p-3 rounded-md"
                        >
                          <CollapsibleTrigger className="flex justify-between items-center w-full">
                            <div className="flex items-center space-x-2">
                              {collapsedQuestions.includes(questionIndex) ? (
                                <ChevronRight className="h-4 w-4" />
                              ) : (
                                <ChevronDown className="h-4 w-4" />
                              )}
                              <span className="font-semibold">Question {question.order}: {question.type}</span>
                            </div>
                            {collapsedQuestions.includes(questionIndex) && (
                              <div className="text-sm text-gray-500">
                                Correct: {question.correctScore} | Incorrect: {question.incorrectScore} | Time: {question.time}min
                              </div>
                            )}
                          </CollapsibleTrigger>
                          <CollapsibleContent>
                            <div className="mt-2">
                              <p><strong>Question:</strong> {question.question}</p>
                              <p><strong>Correct Score:</strong> {question.correctScore}</p>
                              <p><strong>Incorrect Score:</strong> {question.incorrectScore}</p>
                              <p><strong>Time:</strong> {question.time} minutes</p>
                              <div>
                                <strong>Content Links:</strong>
                                <ul className="list-disc pl-5">
                                  {question.contentLinks && question.contentLinks
                                    .sort((a, b) => Number(a.order) - Number(b.order))
                                    .map((item, linkIndex) => (
                                      <li key={linkIndex}>
                                        Order: {item.order}, Link: <a href={item.link} target="_blank" rel="noopener noreferrer" className="text-blue-500 hover:underline">{item.link}</a>
                                      </li>
                                    ))}
                                </ul>
                              </div>
                              <div className="mt-2 space-x-2">
                                <Button onClick={() => handleEdit(questions.findIndex(q => q === question))} variant="outline" size="sm">Edit</Button>
                                <Button onClick={() => handleRemove(questions.findIndex(q => q === question))} variant="destructive" size="sm">Remove</Button>
                              </div>
                            </div>
                          </CollapsibleContent>
                        </Collapsible>
                      ))}
                  </ul>
                )}
              </li>
            );
          })}
        </ul>
      </div>
    </div>
  )
}

