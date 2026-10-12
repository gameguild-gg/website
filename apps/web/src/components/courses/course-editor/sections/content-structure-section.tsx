'use client';

import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@game-guild/ui/components/dialog';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@game-guild/ui/components/dropdown-menu';
import { Input } from '@game-guild/ui/components/input';
import { Switch } from '@game-guild/ui/components/switch';
import { Textarea } from '@game-guild/ui/components/textarea';
import { Activity, BookOpen, CheckSquare, ChevronDown, ChevronRight, Copy, Edit, Eye, EyeOff, File, FileText, Folder, FolderOpen, GripVertical, HelpCircle, MoreHorizontal, Plus, Redo, Trash2, Undo, Video } from 'lucide-react';
import { useState } from 'react';
import { useCourseEditor } from '../../editor/context/course-editor-provider';

type CourseLesson = {
  id: string;
  title: string;
  description: string;
  type: LessonFormData['type'];
  visibility: LessonFormData['visibility'];
  status: LessonFormData['status'];
  isRequired: boolean;
  duration: number;
};

type CourseModule = {
  id: string;
  title: string;
  description: string;
  visibility: ModuleFormData['visibility'];
  status: ModuleFormData['status'];
  estimatedDuration: number;
  isExpanded?: boolean;
  lessons: CourseLesson[];
};

// Content type icons
const CONTENT_TYPE_ICONS = {
  text: FileText,
  video: Video,
  quiz: HelpCircle,
  assignment: CheckSquare,
  file: File,
  interactive: Activity,
} as const;

// Content type colors
const CONTENT_TYPE_COLORS = {
  text: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400',
  video: 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-400',
  quiz: 'bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-400',
  assignment: 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400',
  file: 'bg-gray-100 text-gray-800 dark:bg-gray-900/30 dark:text-gray-400',
  interactive: 'bg-pink-100 text-pink-800 dark:bg-pink-900/30 dark:text-pink-400',
} as const;

interface ModuleFormData {
  title: string;
  description: string;
  visibility: 'public' | 'private' | 'premium';
  status: 'draft' | 'published' | 'archived';
  sortOrder: number;
  estimatedDuration: number;
}

interface LessonFormData {
  title: string;
  description: string;
  type: 'text' | 'video' | 'quiz' | 'assignment' | 'file' | 'interactive';
  visibility: 'public' | 'private' | 'premium';
  status: 'draft' | 'published' | 'archived';
  isRequired: boolean;
  duration: number;
  sortOrder: number;
}

const defaultModuleData: ModuleFormData = {
  title: '',
  description: '',
  visibility: 'public',
  status: 'draft',
  sortOrder: 0,
  estimatedDuration: 60,
};

const defaultLessonData: LessonFormData = {
  title: '',
  description: '',
  type: 'text',
  visibility: 'public',
  status: 'draft',
  isRequired: true,
  duration: 15,
  sortOrder: 0,
};

export function ContentStructureSection() {
  const { state, dispatch } = useCourseEditor();
  const [showModuleDialog, setShowModuleDialog] = useState(false);
  const [showLessonDialog, setShowLessonDialog] = useState(false);
  const [selectedModuleId, setSelectedModuleId] = useState<string | null>(null);
  const [editingModuleId, setEditingModuleId] = useState<string | null>(null);
  const [editingLessonId, setEditingLessonId] = useState<string | null>(null);
  const [moduleFormData, setModuleFormData] = useState<ModuleFormData>(defaultModuleData);
  const [lessonFormData, setLessonFormData] = useState<LessonFormData>(defaultLessonData);  // Handle adding a new module
  const handleSaveModule = () => {
    if (!moduleFormData.title.trim()) return;

    const moduleData = {
      title: moduleFormData.title,
      description: moduleFormData.description,
      sortOrder: state.content.modules.length,
      status: moduleFormData.status,
      visibility: moduleFormData.visibility,
      lessons: [],
      submodules: [],
      estimatedDuration: moduleFormData.estimatedDuration,
    };

    if (editingModuleId) {
      dispatch({ type: 'UPDATE_MODULE', moduleId: editingModuleId, updates: moduleData });
    } else {
      dispatch({ type: 'ADD_MODULE', module: { ...moduleData, lessons: [], submodules: [] } });
    }

    setModuleFormData(defaultModuleData);
    setEditingModuleId(null);
    setShowModuleDialog(false);
  };

  // Handle adding a new lesson
  const handleSaveLesson = () => {
    if (!lessonFormData.title.trim() || !selectedModuleId) return;

    const lessonData = {
      title: lessonFormData.title,
      description: lessonFormData.description,
      type: lessonFormData.type,
      duration: lessonFormData.duration,
      status: lessonFormData.status,
      visibility: lessonFormData.visibility,
      isRequired: lessonFormData.isRequired,
    };

    if (editingLessonId) {
      dispatch({ type: 'UPDATE_LESSON', lessonId: editingLessonId, updates: lessonData });
    } else {
      dispatch({ type: 'ADD_LESSON', moduleId: selectedModuleId, lesson: lessonData });
    }

    setLessonFormData(defaultLessonData);
    setEditingLessonId(null);
    setShowLessonDialog(false);
    setSelectedModuleId(null);
  };

  // Handle module actions
  const handleModuleAction = (moduleId: string, action: string) => {
    const module = state.content.modules.find((m) => m.id === moduleId);

    switch (action) {
      case 'edit':
        if (module) {
          setEditingModuleId(moduleId);
          setModuleFormData({
            title: module.title,
            description: module.description,
            visibility: module.visibility,
            status: module.status,
            sortOrder: module.sortOrder,
            estimatedDuration: module.estimatedDuration,
          });
          setShowModuleDialog(true);
        }
        break;
      case 'delete':
        dispatch({ type: 'REMOVE_MODULE', moduleId });
        break;
      case 'duplicate':
        dispatch({ type: 'DUPLICATE_MODULE', moduleId });
        break;
      case 'toggle-visibility':
        dispatch({
          type: 'UPDATE_MODULE',
          moduleId,
          updates: {
            visibility: state.content.modules.find((m: any) => m.id === moduleId)?.visibility === 'public' ? 'private' : 'public',
          },
        });
        break;
      case 'toggle-expanded':
        dispatch({ type: 'TOGGLE_MODULE_EXPANDED', moduleId });
        break;
    }
  };

  // Handle lesson actions
  const handleLessonAction = (lessonId: string, action: string) => {
    const owningModule = state.content.modules.find((module) => module.lessons.some((lesson) => lesson.id === lessonId));
    const lesson = owningModule?.lessons.find((item) => item.id === lessonId);

    switch (action) {
      case 'edit':
        if (owningModule && lesson) {
          setSelectedModuleId(owningModule.id);
          setEditingLessonId(lessonId);
          setLessonFormData({
            title: lesson.title,
            description: lesson.description,
            type: lesson.type,
            visibility: lesson.visibility,
            status: lesson.status,
            isRequired: lesson.isRequired,
            duration: lesson.duration,
            sortOrder: 0,
          });
          setShowLessonDialog(true);
        }
        break;
      case 'delete':
        dispatch({ type: 'REMOVE_LESSON', lessonId });
        break;
      case 'duplicate':
        dispatch({ type: 'DUPLICATE_LESSON', lessonId });
        break;
      case 'toggle-visibility':
        if (lesson) {
          dispatch({
            type: 'UPDATE_LESSON',
            lessonId,
            updates: {
              visibility: lesson.visibility === 'public' ? 'private' : 'public',
            },
          });
        }
        break;
    }
  };

  // Render lesson item
  const renderLesson = (lesson: CourseLesson) => {
    const IconComponent = CONTENT_TYPE_ICONS[lesson.type as keyof typeof CONTENT_TYPE_ICONS] || FileText;
    const colorClass = CONTENT_TYPE_COLORS[lesson.type as keyof typeof CONTENT_TYPE_COLORS] || 'text-slate-400';

    return (
      <div
        key={lesson.id}
        className={`group flex items-center gap-3 p-3 rounded-lg border transition-all hover:shadow-sm ${state.content.selectedItems.includes(lesson.id) ? 'bg-primary/5 border-primary/20' : 'bg-background border-border hover:border-border/60'
          }`}
      >
        {/* Drag handle */}
        <GripVertical className="h-4 w-4 text-muted-foreground opacity-0 group-hover:opacity-100 cursor-grab" />

        {/* Content type icon */}
        <div className={`p-1.5 rounded ${colorClass}`}>
          <IconComponent className="h-3 w-3" />
        </div>

        {/* Lesson info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <h4 className="text-sm font-medium truncate">{lesson.title}</h4>
            {lesson.isRequired && (
              <Badge variant="secondary" className="text-xs">
                Required
              </Badge>
            )}
            {lesson.visibility === 'private' && <EyeOff className="h-3 w-3 text-muted-foreground" />}
          </div>
          {lesson.description && <p className="text-xs text-muted-foreground truncate mt-1">{lesson.description}</p>}
          <div className="flex items-center gap-2 mt-1">
            <Badge variant="outline" className="text-xs">
              {lesson.type}
            </Badge>
            <span className="text-xs text-muted-foreground">{lesson.duration}m</span>
            <Badge variant={lesson.status === 'published' ? 'default' : 'secondary'} className="text-xs">
              {lesson.status}
            </Badge>
          </div>
        </div>

        {/* Actions */}
        <DropdownMenu>
          <DropdownMenuTrigger render={<Button variant="ghost" size="sm" className="h-8 w-8 p-0 opacity-0 group-hover:opacity-100" />}>
            <MoreHorizontal className="h-4 w-4" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-48">
            <DropdownMenuItem onClick={() => handleLessonAction(lesson.id, 'edit')}>
              <Edit className="h-4 w-4 mr-2" />
              Edit Lesson
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => handleLessonAction(lesson.id, 'duplicate')}>
              <Copy className="h-4 w-4 mr-2" />
              Duplicate
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => handleLessonAction(lesson.id, 'toggle-visibility')}>
              {lesson.visibility === 'public' ? (
                <>
                  <EyeOff className="h-4 w-4 mr-2" />
                  Make Private
                </>
              ) : (
                <>
                  <Eye className="h-4 w-4 mr-2" />
                  Make Public
                </>
              )}
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => handleLessonAction(lesson.id, 'delete')} className="text-destructive focus:text-destructive">
              <Trash2 className="h-4 w-4 mr-2" />
              Delete
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    );
  };

  // Render module item
  const renderModule = (module: CourseModule) => {
    const isExpanded = module.isExpanded ?? false;
    const lessonCount = module.lessons?.length ?? 0;
    const totalDuration = module.lessons?.reduce((sum: number, lesson: any) => sum + lesson.duration, 0) ?? 0;

    return (
      <div key={module.id} className="border border-border rounded-lg overflow-hidden">
        {/* Module header */}
        <div className={`group flex items-center gap-3 p-4 transition-all hover:bg-muted/30 ${state.content.selectedItems.includes(module.id) ? 'bg-primary/5 border-primary/20' : 'bg-background'}`}>
          {/* Drag handle */}
          <GripVertical className="h-4 w-4 text-muted-foreground opacity-0 group-hover:opacity-100 cursor-grab" />

          {/* Expand/collapse button */}
          <Button variant="ghost" size="sm" className="h-6 w-6 p-0" onClick={() => handleModuleAction(module.id, 'toggle-expanded')}>
            {isExpanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
          </Button>

          {/* Module icon */}
          <div className="p-1.5 rounded bg-primary/10 text-primary">{isExpanded ? <FolderOpen className="h-4 w-4" /> : <Folder className="h-4 w-4" />}</div>

          {/* Module info */}
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2">
              <h3 className="font-medium truncate">{module.title}</h3>
              {module.visibility === 'private' && <EyeOff className="h-4 w-4 text-muted-foreground" />}
            </div>
            {module.description && <p className="text-sm text-muted-foreground truncate mt-1">{module.description}</p>}
            <div className="flex items-center gap-3 mt-2">
              <span className="text-xs text-muted-foreground">
                {lessonCount} lesson{lessonCount !== 1 ? 's' : ''}
              </span>
              <span className="text-xs text-muted-foreground">~{Math.round(totalDuration)}m</span>
              <Badge variant={module.status === 'published' ? 'default' : 'secondary'} className="text-xs">
                {module.status}
              </Badge>
            </div>
          </div>

          {/* Add lesson button */}
          <Button
            variant="outline"
            size="sm"
            className="opacity-0 group-hover:opacity-100 transition-opacity"
            onClick={() => {
              setSelectedModuleId(module.id);
              setShowLessonDialog(true);
            }}
          >
            <Plus className="h-4 w-4 mr-2" />
            Add Lesson
          </Button>

          {/* Module actions */}
          <DropdownMenu>
            <DropdownMenuTrigger render={<Button variant="ghost" size="sm" className="h-8 w-8 p-0 opacity-0 group-hover:opacity-100" />}>
              <MoreHorizontal className="h-4 w-4" />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-48">
              <DropdownMenuItem onClick={() => handleModuleAction(module.id, 'edit')}>
                <Edit className="h-4 w-4 mr-2" />
                Edit Module
              </DropdownMenuItem>
              <DropdownMenuItem onClick={() => handleModuleAction(module.id, 'duplicate')}>
                <Copy className="h-4 w-4 mr-2" />
                Duplicate
              </DropdownMenuItem>
              <DropdownMenuItem onClick={() => handleModuleAction(module.id, 'toggle-visibility')}>
                {module.visibility === 'public' ? (
                  <>
                    <EyeOff className="h-4 w-4 mr-2" />
                    Make Private
                  </>
                ) : (
                  <>
                    <Eye className="h-4 w-4 mr-2" />
                    Make Public
                  </>
                )}
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={() => handleModuleAction(module.id, 'delete')} className="text-destructive focus:text-destructive">
                <Trash2 className="h-4 w-4 mr-2" />
                Delete
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>

        {/* Module lessons (when expanded) */}
        {isExpanded && (
          <div className="p-4 pt-0 space-y-2 bg-muted/20">
            {module.lessons && module.lessons.length > 0 ? (
              module.lessons.map((lesson: any) => renderLesson(lesson))
            ) : (
              <div className="text-center py-8 text-muted-foreground">
                <BookOpen className="h-8 w-8 mx-auto mb-2 opacity-50" />
                <p className="text-sm">No lessons yet</p>
                <Button
                  variant="outline"
                  size="sm"
                  className="mt-2"
                  onClick={() => {
                    setSelectedModuleId(module.id);
                    setShowLessonDialog(true);
                  }}
                >
                  <Plus className="h-4 w-4 mr-2" />
                  Add First Lesson
                </Button>
              </div>
            )}
          </div>
        )}
      </div>
    );
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold">Course Content</h2>
          <p className="text-sm text-muted-foreground">Organize your course into modules and lessons</p>
        </div>

        <div className="flex items-center gap-2">
          {/* Undo/Redo */}
          <Button variant="outline" size="sm" disabled={!state.undoRedo.canUndo} onClick={() => dispatch({ type: 'UNDO' })}>
            <Undo className="h-4 w-4" />
          </Button>
          <Button variant="outline" size="sm" disabled={!state.undoRedo.canRedo} onClick={() => dispatch({ type: 'REDO' })}>
            <Redo className="h-4 w-4" />
          </Button>

          {/* Add Module Button */}
          <Button onClick={() => setShowModuleDialog(true)} className="bg-gradient-to-r from-primary to-chart-2">
            <Plus className="h-4 w-4 mr-2" />
            Add Module
          </Button>
        </div>
      </div>

      {/* Content */}
      {state.content.modules.length > 0 ? (
        <div className="space-y-4">{state.content.modules.map(renderModule)}</div>
      ) : (
        <div className="text-center py-12 border-2 border-dashed border-border rounded-lg">
          <BookOpen className="h-12 w-12 mx-auto mb-4 text-muted-foreground/50" />
          <h3 className="text-lg font-medium mb-2">No modules yet</h3>
          <p className="text-muted-foreground mb-4">Start building your course by adding your first module</p>
          <Button onClick={() => setShowModuleDialog(true)} className="bg-gradient-to-r from-primary to-chart-2">
            <Plus className="h-4 w-4 mr-2" />
            Add First Module
          </Button>
        </div>
      )}

      {/* Add Module Dialog */}
      <Dialog open={showModuleDialog} onOpenChange={setShowModuleDialog}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{editingModuleId ? 'Edit Module' : 'Add New Module'}</DialogTitle>
            <DialogDescription>Define the module name, visibility, status, and expected duration.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <label className="text-sm font-medium">Title</label>
              <Input placeholder="Module title..." value={moduleFormData.title} onChange={(e) => setModuleFormData({ ...moduleFormData, title: e.target.value })} />
            </div>

            <div>
              <label className="text-sm font-medium">Description</label>
              <Textarea placeholder="Brief description of this module..." value={moduleFormData.description} onChange={(e) => setModuleFormData({ ...moduleFormData, description: e.target.value })} rows={3} />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="text-sm font-medium">Visibility</label>
                <select
                  className="w-full mt-1 px-3 py-2 border border-border rounded-md bg-background"
                  value={moduleFormData.visibility}
                  onChange={(e) =>
                    setModuleFormData({
                      ...moduleFormData,
                      visibility: e.target.value as 'public' | 'private' | 'premium',
                    })
                  }
                >
                  <option value="public">Public</option>
                  <option value="private">Private</option>
                  <option value="premium">Premium</option>
                </select>
              </div>

              <div>
                <label className="text-sm font-medium">Status</label>
                <select
                  className="w-full mt-1 px-3 py-2 border border-border rounded-md bg-background"
                  value={moduleFormData.status}
                  onChange={(e) =>
                    setModuleFormData({
                      ...moduleFormData,
                      status: e.target.value as 'draft' | 'published' | 'archived',
                    })
                  }
                >
                  <option value="draft">Draft</option>
                  <option value="published">Published</option>
                  <option value="archived">Archived</option>
                </select>
              </div>
            </div>

            <div className="flex justify-end gap-2 pt-4">
              <Button
                variant="outline"
                onClick={() => {
                  setEditingModuleId(null);
                  setModuleFormData(defaultModuleData);
                  setShowModuleDialog(false);
                }}
              >
                Cancel
              </Button>
              <Button onClick={handleSaveModule} disabled={!moduleFormData.title.trim()}>
                {editingModuleId ? 'Save Module' : 'Add Module'}
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>

      {/* Add Lesson Dialog */}
      <Dialog open={showLessonDialog} onOpenChange={setShowLessonDialog}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{editingLessonId ? 'Edit Lesson' : 'Add New Lesson'}</DialogTitle>
            <DialogDescription>Define the lesson metadata students will see in the course outline.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <label className="text-sm font-medium">Title</label>
              <Input placeholder="Lesson title..." value={lessonFormData.title} onChange={(e) => setLessonFormData({ ...lessonFormData, title: e.target.value })} />
            </div>

            <div>
              <label className="text-sm font-medium">Description</label>
              <Textarea placeholder="Brief description of this lesson..." value={lessonFormData.description} onChange={(e) => { setLessonFormData({ ...lessonFormData, description: e.target.value }); }} rows={3} />
            </div>

            <div>
              <label className="text-sm font-medium">Content Type</label>
              <select
                className="w-full mt-1 px-3 py-2 border border-border rounded-md bg-background"
                value={lessonFormData.type}
                onChange={(e) =>
                  setLessonFormData({
                    ...lessonFormData,
                    type: e.target.value as 'text' | 'video' | 'quiz' | 'assignment' | 'file' | 'interactive',
                  })
                }
              >
                <option value="text">Text/Article</option>
                <option value="video">Video</option>
                <option value="quiz">Quiz</option>
                <option value="assignment">Assignment</option>
                <option value="file">File/Download</option>
                <option value="interactive">Interactive</option>
              </select>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="text-sm font-medium">Duration (minutes)</label>
                <Input
                  type="number"
                  min="1"
                  value={lessonFormData.duration}
                  onChange={(e) =>
                    setLessonFormData({
                      ...lessonFormData,
                      duration: parseInt(e.target.value) || 15,
                    })
                  }
                />
              </div>

              <div>
                <label className="text-sm font-medium">Status</label>
                <select
                  className="w-full mt-1 px-3 py-2 border border-border rounded-md bg-background"
                  value={lessonFormData.status}
                  onChange={(e) =>
                    setLessonFormData({
                      ...lessonFormData,
                      status: e.target.value as 'draft' | 'published' | 'archived',
                    })
                  }
                >
                  <option value="draft">Draft</option>
                  <option value="published">Published</option>
                  <option value="archived">Archived</option>
                </select>
              </div>
            </div>

            <div className="flex items-center justify-between">
              <div className="flex items-center space-x-2">
                <Switch
                  checked={lessonFormData.isRequired}
                  onCheckedChange={(checked) =>
                    setLessonFormData({
                      ...lessonFormData,
                      isRequired: checked,
                    })
                  }
                />
                <label className="text-sm font-medium">Required lesson</label>
              </div>
            </div>

            <div className="flex justify-end gap-2 pt-4">
              <Button
                variant="outline"
                onClick={() => {
                  setEditingLessonId(null);
                  setSelectedModuleId(null);
                  setLessonFormData(defaultLessonData);
                  setShowLessonDialog(false);
                }}
              >
                Cancel
              </Button>
              <Button onClick={handleSaveLesson} disabled={!lessonFormData.title.trim()}>
                {editingLessonId ? 'Save Lesson' : 'Add Lesson'}
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
