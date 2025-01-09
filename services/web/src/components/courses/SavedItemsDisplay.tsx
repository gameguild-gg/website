import React, { useState } from 'react'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible"
import { Button } from "@/components/ui/button"
import { ChevronDown, ChevronRight } from 'lucide-react'

interface SavedItemsDisplayProps {
  items: any[];
  itemType: 'module' | 'lesson' | 'exercise' | 'question';
  onEdit: (index: number) => void;
  onRemove: (index: number) => void;
  getChildItems?: (item: any) => any[];
  renderAdditionalInfo?: (item: any) => React.ReactNode;
  renderSpecificFields?: (item: any) => React.ReactNode;
  groupByParent?: boolean;
  parentItems?: any[];
  grandParentItems?: any[];
}

const SavedItemsDisplay: React.FC<SavedItemsDisplayProps> = ({
  items,
  itemType,
  onEdit,
  onRemove,
  getChildItems,
  renderAdditionalInfo,
  renderSpecificFields,
  groupByParent = false,
  parentItems = [],
  grandParentItems = []
}) => {
  const [expandedItems, setExpandedItems] = useState<string[]>([])

  const toggleItem = (itemId: string) => {
    setExpandedItems(prev => 
      prev.includes(itemId)
        ? prev.filter(id => id !== itemId)
        : [...prev, itemId]
    )
  }

  const renderModuleItem = (item: any, depth: number = 0) => {
    const childItems = getChildItems ? getChildItems(item) : [];
    const isExpanded = expandedItems.includes(item.id);

    return (
      <li key={item.id} className="bg-gray-100 p-4 rounded-md">
        <Collapsible
          open={isExpanded}
          onOpenChange={() => toggleItem(item.id)}
        >
          <CollapsibleTrigger className="flex items-center justify-between w-full">
            <span className="font-semibold">
              {isExpanded ? <ChevronDown className="inline mr-2" /> : <ChevronRight className="inline mr-2" />}
              Module {item.order}: {item.title}
            </span>
            <span>{childItems.length} submodule(s)</span>
          </CollapsibleTrigger>
          <CollapsibleContent>
            <div className="mt-2 space-y-2">
              <div className="flex justify-between items-center mb-2">
                <div>
                  <p className="text-sm text-gray-600">{item.description}</p>
                  <p className="text-xs text-gray-500">ID: {item.id}</p>
                  {renderAdditionalInfo && renderAdditionalInfo(item)}
                </div>
                <div className="space-x-2">
                  <Button onClick={() => onEdit(items.findIndex(i => i.id === item.id))} variant="outline" size="sm">Edit</Button>
                  <Button onClick={() => onRemove(items.findIndex(i => i.id === item.id))} variant="destructive" size="sm">Remove</Button>
                </div>
              </div>
              {renderSpecificFields && renderSpecificFields(item)}
              {childItems.length > 0 && (
                <ul className="mt-2 space-y-2 pl-4">
                  {childItems.map(childItem => renderModuleItem(childItem, depth + 1))}
                </ul>
              )}
            </div>
          </CollapsibleContent>
        </Collapsible>
      </li>
    );
  }

  const renderLessonItem = (item: any) => {
    const isExpanded = expandedItems.includes(item.id);
    const module = parentItems.find(m => m.id === item.moduleId);

    return (
      <li key={item.id} className="bg-white p-3 rounded-md">
        <Collapsible
          open={isExpanded}
          onOpenChange={() => toggleItem(item.id)}
        >
          <CollapsibleTrigger className="flex items-center justify-between w-full">
            <span className="font-semibold">
              {isExpanded ? <ChevronDown className="inline mr-2" /> : <ChevronRight className="inline mr-2" />}
              Lesson {item.order}: {item.title}
            </span>
          </CollapsibleTrigger>
          <CollapsibleContent>
            <div className="mt-2">
              <div className="flex justify-between items-center mb-2">
                <div>
                  <p className="text-sm text-gray-600">{item.description}</p>
                  <p className="text-xs text-gray-500">Module: {module ? module.title : 'Unassigned'}</p>
                </div>
                <div className="space-x-2">
                  <Button onClick={() => onEdit(items.findIndex(i => i.id === item.id))} variant="outline" size="sm">Edit</Button>
                  <Button onClick={() => onRemove(items.findIndex(i => i.id === item.id))} variant="destructive" size="sm">Remove</Button>
                </div>
              </div>
              {renderAdditionalInfo && renderAdditionalInfo(item)}
              {renderSpecificFields && renderSpecificFields(item)}
            </div>
          </CollapsibleContent>
        </Collapsible>
      </li>
    );
  }

  const renderExerciseItem = (item: any) => {
    const isExpanded = expandedItems.includes(item.id);
    const lesson = parentItems.find(l => l.id === item.lessonId);
    const module = grandParentItems.find(m => m.id === item.moduleId);

    return (
      <li key={item.id} className="bg-white p-3 rounded-md">
        <Collapsible
          open={isExpanded}
          onOpenChange={() => toggleItem(item.id)}
        >
          <CollapsibleTrigger className="flex items-center justify-between w-full">
            <span className="font-semibold">
              {isExpanded ? <ChevronDown className="inline mr-2" /> : <ChevronRight className="inline mr-2" />}
              Exercise {item.order}: {item.title}
            </span>
          </CollapsibleTrigger>
          <CollapsibleContent>
            <div className="mt-2">
              <div className="flex justify-between items-center mb-2">
                <div>
                  <p className="text-sm text-gray-600">{item.description}</p>
                  <p className="text-xs text-gray-500">
                    Module: {module ? module.title : 'Unassigned'}, 
                    Lesson: {lesson ? lesson.title : 'Unassigned'}
                  </p>
                </div>
                <div className="space-x-2">
                  <Button onClick={() => onEdit(items.findIndex(i => i.id === item.id))} variant="outline" size="sm">Edit</Button>
                  <Button onClick={() => onRemove(items.findIndex(i => i.id === item.id))} variant="destructive" size="sm">Remove</Button>
                </div>
              </div>
              {renderAdditionalInfo && renderAdditionalInfo(item)}
              {renderSpecificFields && renderSpecificFields(item)}
            </div>
          </CollapsibleContent>
        </Collapsible>
      </li>
    );
  }

  const renderGroupedItems = () => {
    if (itemType === 'lesson') {
      const groupedLessons = items.reduce((acc, lesson) => {
        const moduleId = lesson.moduleId || 'Unassigned';
        if (!acc[moduleId]) {
          acc[moduleId] = [];
        }
        acc[moduleId].push(lesson);
        return acc;
      }, {} as Record<string, any[]>);

      const sortedModules = [...parentItems].sort((a, b) => Number(a.order) - Number(b.order));

      return (
        <>
          {sortedModules.map((module) => {
            const moduleLessons = groupedLessons[module.id] || [];
            if (moduleLessons.length === 0) return null;

            const isExpanded = expandedItems.includes(module.id);

            return (
              <li key={module.id} className="bg-gray-100 p-4 rounded-md">
                <Collapsible
                  open={isExpanded}
                  onOpenChange={() => toggleItem(module.id)}
                >
                  <CollapsibleTrigger className="flex items-center justify-between w-full">
                    <span className="font-semibold">
                      {isExpanded ? <ChevronDown className="inline mr-2" /> : <ChevronRight className="inline mr-2" />}
                      Module {module.order}: {module.title}
                    </span>
                    <span>{moduleLessons.length} lesson(s)</span>
                  </CollapsibleTrigger>
                  <CollapsibleContent>
                    <ul className="mt-2 space-y-2">
                      {moduleLessons
                        .sort((a, b) => Number(a.order) - Number(b.order))
                        .map(lesson => renderLessonItem(lesson))}
                    </ul>
                  </CollapsibleContent>
                </Collapsible>
              </li>
            );
          })}
          {groupedLessons['Unassigned'] && groupedLessons['Unassigned'].length > 0 && (
            <li className="bg-gray-100 p-4 rounded-md">
              <Collapsible
                open={expandedItems.includes('Unassigned')}
                onOpenChange={() => toggleItem('Unassigned')}
              >
                <CollapsibleTrigger className="flex items-center justify-between w-full">
                  <span className="font-semibold">
                    {expandedItems.includes('Unassigned') ? <ChevronDown className="inline mr-2" /> : <ChevronRight className="inline mr-2" />}
                    Unassigned Lessons
                  </span>
                  <span>{groupedLessons['Unassigned'].length} lesson(s)</span>
                </CollapsibleTrigger>
                <CollapsibleContent>
                  <ul className="mt-2 space-y-2">
                    {groupedLessons['Unassigned']
                      .sort((a, b) => Number(a.order) - Number(b.order))
                      .map(lesson => renderLessonItem(lesson))}
                  </ul>
                </CollapsibleContent>
              </Collapsible>
            </li>
          )}
        </>
      );
    } else if (itemType === 'exercise') {
      const groupedExercises = items.reduce((acc, exercise) => {
        const moduleId = exercise.moduleId || 'Unassigned';
        const lessonId = exercise.lessonId || 'Unassigned';
        if (!acc[moduleId]) {
          acc[moduleId] = {};
        }
        if (!acc[moduleId][lessonId]) {
          acc[moduleId][lessonId] = [];
        }
        acc[moduleId][lessonId].push(exercise);
        return acc;
      }, {} as Record<string, Record<string, any[]>>);

      const sortedModules = [...grandParentItems].sort((a, b) => Number(a.order) - Number(b.order));

      return (
        <>
          {sortedModules.map((module) => {
            const moduleExercises = groupedExercises[module.id];
            if (!moduleExercises) return null;

            const isModuleExpanded = expandedItems.includes(module.id);

            return (
              <li key={module.id} className="bg-gray-100 p-4 rounded-md">
                <Collapsible
                  open={isModuleExpanded}
                  onOpenChange={() => toggleItem(module.id)}
                >
                  <CollapsibleTrigger className="flex items-center justify-between w-full">
                    <span className="font-semibold">
                      {isModuleExpanded ? <ChevronDown className="inline mr-2" /> : <ChevronRight className="inline mr-2" />}
                      Module {module.order}: {module.title}
                    </span>
                    <span>{Object.values(moduleExercises).flat().length} exercise(s)</span>
                  </CollapsibleTrigger>
                  <CollapsibleContent>
                    <ul className="mt-2 space-y-2">
                      {Object.entries(moduleExercises).map(([lessonId, lessonExercises]) => {
                        const lesson = parentItems.find(l => l.id === lessonId) || { title: 'Unassigned', id: 'Unassigned' };
                        const isLessonExpanded = expandedItems.includes(lesson.id);

                        return (
                          <li key={lesson.id} className="bg-white p-3 rounded-md">
                            <Collapsible
                              open={isLessonExpanded}
                              onOpenChange={() => toggleItem(lesson.id)}
                            >
                              <CollapsibleTrigger className="flex items-center justify-between w-full">
                                <span className="font-semibold">
                                  {isLessonExpanded ? <ChevronDown className="inline mr-2" /> : <ChevronRight className="inline mr-2" />}
                                  {lesson.id === 'Unassigned' ? 'Unassigned to Lesson' : `Lesson: ${lesson.title}`}
                                </span>
                                <span>{lessonExercises.length} exercise(s)</span>
                              </CollapsibleTrigger>
                              <CollapsibleContent>
                                <ul className="mt-2 space-y-2">
                                  {lessonExercises
                                    .sort((a, b) => Number(a.order) - Number(b.order))
                                    .map(exercise => renderExerciseItem(exercise))}
                                </ul>
                              </CollapsibleContent>
                            </Collapsible>
                          </li>
                        );
                      })}
                    </ul>
                  </CollapsibleContent>
                </Collapsible>
              </li>
            );
          })}
          {groupedExercises['Unassigned'] && Object.keys(groupedExercises['Unassigned']).length > 0 && (
            <li className="bg-gray-100 p-4 rounded-md">
              <Collapsible
                open={expandedItems.includes('Unassigned')}
                onOpenChange={() => toggleItem('Unassigned')}
              >
                <CollapsibleTrigger className="flex items-center justify-between w-full">
                  <span className="font-semibold">
                    {expandedItems.includes('Unassigned') ? <ChevronDown className="inline mr-2" /> : <ChevronRight className="inline mr-2" />}
                    Unassigned Exercises
                  </span>
                  <span>{Object.values(groupedExercises['Unassigned']).flat().length} exercise(s)</span>
                </CollapsibleTrigger>
                <CollapsibleContent>
                  <ul className="mt-2 space-y-2">
                    {Object.entries(groupedExercises['Unassigned']).map(([lessonId, lessonExercises]) => {
                      const lesson = parentItems.find(l => l.id === lessonId) || { title: 'Unassigned', id: 'Unassigned' };
                      const isLessonExpanded = expandedItems.includes(lesson.id);

                      return (
                        <li key={lesson.id} className="bg-white p-3 rounded-md">
                          <Collapsible
                            open={isLessonExpanded}
                            onOpenChange={() => toggleItem(lesson.id)}
                          >
                            <CollapsibleTrigger className="flex items-center justify-between w-full">
                              <span className="font-semibold">
                                {isLessonExpanded ? <ChevronDown className="inline mr-2" /> : <ChevronRight className="inline mr-2" />}
                                {lesson.id === 'Unassigned' ? 'Unassigned to Lesson' : `Lesson: ${lesson.title}`}
                              </span>
                              <span>{lessonExercises.length} exercise(s)</span>
                            </CollapsibleTrigger>
                            <CollapsibleContent>
                              <ul className="mt-2 space-y-2">
                                {lessonExercises
                                  .sort((a, b) => Number(a.order) - Number(b.order))
                                  .map(exercise => renderExerciseItem(exercise))}
                              </ul>
                            </CollapsibleContent>
                          </Collapsible>
                        </li>
                      );
                    })}
                  </ul>
                </CollapsibleContent>
              </Collapsible>
            </li>
          )}
        </>
      );
    }
  }

  return (
    <div>
      <h3 className="text-lg font-semibold mb-2">Saved {itemType.charAt(0).toUpperCase() + itemType.slice(1)}s</h3>
      <ul className="space-y-2">
        {itemType === 'module' && items.filter(item => !item.parentId).map(item => renderModuleItem(item))}
        {(itemType === 'lesson' || itemType === 'exercise') && (groupByParent ? renderGroupedItems() : items.map(itemType === 'lesson' ? renderLessonItem : renderExerciseItem))}
      </ul>
    </div>
  );
}

export default SavedItemsDisplay;

