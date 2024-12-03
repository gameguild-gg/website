import { useState } from 'react'
import { DragDropContext, Droppable, Draggable } from 'react-beautiful-dnd'
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { useCourseStore } from '@/store/courseStore'
import { LessonForm } from './lesson-form'

export function LessonList() {
  const { lessons, reorderLessons, updateLesson } = useCourseStore()
  const [editingLessonId, setEditingLessonId] = useState<string | null>(null)

  const onDragEnd = (result: any) => {
    if (!result.destination) return
    reorderLessons(result.source.index, result.destination.index)
  }

  return (
    <DragDropContext onDragEnd={onDragEnd}>
      <Droppable droppableId="lessons">
        {(provided) => (
          <div {...provided.droppableProps} ref={provided.innerRef} className="space-y-4">
            {lessons.map((lesson, index) => (
              <Draggable key={lesson.id} draggableId={lesson.id} index={index}>
                {(provided) => (
                  <Card
                    ref={provided.innerRef}
                    {...provided.draggableProps}
                    {...provided.dragHandleProps}
                  >
                    <CardContent className="p-4">
                      {editingLessonId === lesson.id ? (
                        <LessonForm
                          lesson={lesson}
                          onSave={(updatedLesson) => {
                            updateLesson(lesson.id, updatedLesson)
                            setEditingLessonId(null)
                          }}
                          onCancel={() => setEditingLessonId(null)}
                        />
                      ) : (
                        <>
                          <h3 className="font-semibold">
                            {lesson.order}. {lesson.title}
                          </h3>
                          <p className="text-sm text-muted-foreground">
                            {lesson.description.substring(0, 100)}
                            {lesson.description.length > 100 ? '...' : ''}
                          </p>
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => setEditingLessonId(lesson.id)}
                            className="mt-2"
                          >
                            Edit
                          </Button>
                        </>
                      )}
                    </CardContent>
                  </Card>
                )}
              </Draggable>
            ))}
            {provided.placeholder}
          </div>
        )}
      </Droppable>
    </DragDropContext>
  )
}

