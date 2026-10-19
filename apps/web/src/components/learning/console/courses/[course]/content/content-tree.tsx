"use client";

import {
  addContent,
  deleteContent,
  moveContent,
  reorderContent,
  updateContent,
} from "@/lib/learning/actions";
import type {
  ContentItem,
  LearningCoursesProgramContentType,
} from "@/lib/learning/types";
import {
  DEFAULT_LESSON_FORMAT,
  LESSON_FORMATS,
  type LessonContentFormat,
} from "@/lib/learning/lesson-formats";
import type { DragEndEvent, DragStartEvent } from "@dnd-kit/core";
import {
  closestCorners,
  DndContext,
  DragOverlay,
  PointerSensor,
  useDroppable,
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import {
  arrayMove,
  SortableContext,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { Badge } from "@game-guild/ui/components/badge";
import { Button } from "@game-guild/ui/components/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@game-guild/ui/components/card";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@game-guild/ui/components/collapsible";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@game-guild/ui/components/dialog";
import { Input } from "@game-guild/ui/components/input";
import { Label } from "@game-guild/ui/components/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@game-guild/ui/components/select";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@game-guild/ui/components/tooltip";
import {
  ArrowDown,
  ArrowUp,
  BookOpen,
  ChevronDown,
  ChevronRight,
  Clock,
  Code2,
  Copy,
  Edit,
  FileText,
  Flag,
  GripVertical,
  HelpCircle,
  Loader2,
  MessageSquare,
  Plus,
  Trash2,
} from "lucide-react";
import { usePathname, useRouter } from "next/navigation";
import React, { useEffect, useRef, useState, useTransition } from "react";
import { normalizeSlug, slugify } from "@/lib/slugify";

interface ContentTreeProps {
  courseId: string;
  modules: ContentItem[];
  allItems: ContentItem[];
  virtualModuleIds?: string[];
}

const typeConfig: Record<
  LearningCoursesProgramContentType,
  { icon: React.ElementType; label: string }
> = {
  Module: { icon: BookOpen, label: "Module" },
  Lesson: { icon: FileText, label: "Lesson" },
  Assignment: { icon: FileText, label: "Assignment" },
  Questionnaire: { icon: HelpCircle, label: "Quiz" },
  Discussion: { icon: MessageSquare, label: "Discussion" },
  Code: { icon: Code2, label: "Code" },
  Reflection: { icon: BookOpen, label: "Reflection" },
  Survey: { icon: HelpCircle, label: "Survey" },
  Project: { icon: Flag, label: "Project" },
};

const visibilityVariant: Record<string, "default" | "secondary" | "outline"> = {
  Public: "default",
  Private: "secondary",
  Internal: "outline",
  Restricted: "outline",
};

// Lesson types available when adding a new lesson (backend ProgramContentType values)
const lessonTypes: Array<{
  value: LearningCoursesProgramContentType;
  label: string;
}> = [
  { value: "Lesson", label: "Lesson" },
  { value: "Questionnaire", label: "Quiz" },
  { value: "Project", label: "Project" },
  { value: "Discussion", label: "Discussion" },
  { value: "Code", label: "Code" },
  { value: "Reflection", label: "Reflection" },
  { value: "Survey", label: "Survey" },
];

function SortableItem({
  id,
  children,
}: {
  id: string;
  children: (props: {
    ref: (el: HTMLElement | null) => void;
    style: React.CSSProperties;
    listeners: ReturnType<typeof useSortable>["listeners"];
    isDragging: boolean;
  }) => React.ReactNode;
}) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id });
  // ponytail: freeze source at origin while DragOverlay (root-level) follows
  // pointer. useSortable's transform only animates inside its own
  // SortableContext — without this, the dragged item vanishes when crossing
  // into another module's container.
  const style: React.CSSProperties = isDragging
    ? { opacity: 0 }
    : {
        transform: CSS.Transform.toString(transform),
        transition,
      };
  return <>{children({ ref: setNodeRef, style, listeners, isDragging })}</>;
}

function DroppableCardArea({
  moduleId,
  children,
}: {
  moduleId: string;
  children: React.ReactNode;
}) {
  const { setNodeRef } = useDroppable({ id: `module-drop-${moduleId}` });
  return <div ref={setNodeRef}>{children}</div>;
}

function ContentActionButton({
  label,
  icon: Icon,
  onClick,
  disabled,
  destructive = false,
  className = "size-8",
}: {
  label: string;
  icon: React.ElementType;
  onClick: () => void;
  disabled?: boolean;
  destructive?: boolean;
  className?: string;
}) {
  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className={`${className} ${destructive ? "text-destructive hover:text-destructive" : ""}`}
            onClick={(event) => {
              event.stopPropagation();
              onClick();
            }}
            disabled={disabled}
            aria-label={label}
          />
        }
      >
        <Icon className="size-4" />
      </TooltipTrigger>
      <TooltipContent>{label}</TooltipContent>
    </Tooltip>
  );
}

export function ContentTree({
  courseId,
  modules,
  allItems,
  virtualModuleIds = [],
}: ContentTreeProps) {
  const router = useRouter();
  const pathname = usePathname();
  const [isPending, startTransition] = useTransition();
  const [openModules, setOpenModules] = useState<Set<string>>(
    () => new Set(modules.map((m) => m.id)),
  );
  const previousModuleIds = useRef(new Set(modules.map((module) => module.id)));
  const virtualModuleIdSet = React.useMemo(
    () => new Set(virtualModuleIds),
    [virtualModuleIds],
  );

  useEffect(() => {
    const nextModuleIds = new Set(modules.map((module) => module.id));
    const priorModuleIds = previousModuleIds.current;

    setOpenModules((current) => {
      const next = new Set([...current].filter((id) => nextModuleIds.has(id)));
      for (const id of nextModuleIds) {
        if (!priorModuleIds.has(id)) next.add(id);
      }
      return next;
    });

    previousModuleIds.current = nextModuleIds;
  }, [modules]);

  // Derive the base path for navigation (e.g. /en-US/learning/courses/{id}/content)
  const contentBasePath = pathname.endsWith("/content")
    ? pathname
    : pathname.replace(/\/content\/.*$/, "/content");

  const navigateToContentItem = (contentId: string) => {
    router.push(
      `${contentBasePath}/${contentId}` as Parameters<typeof router.push>[0],
    );
  };

  const isVirtualModule = (moduleId: string) =>
    virtualModuleIdSet.has(moduleId);
  const normalizeParentId = (
    parentId: string | null | undefined,
  ): string | undefined => {
    if (!parentId || virtualModuleIdSet.has(parentId)) {
      return undefined;
    }

    return parentId;
  };

  // Real modules are draggable + sortable. Virtual modules (e.g. the synthetic
  // "Unassigned" bucket) render outside the SortableContext and never reach the
  // reorder API — sending a virtual id to /content/reorder snaps the drag back.
  const { realModules, virtualModules } = React.useMemo(() => {
    const real: ContentItem[] = [];
    const virtual: ContentItem[] = [];
    for (const module of modules) {
      if (virtualModuleIdSet.has(module.id)) virtual.push(module);
      else real.push(module);
    }
    return { realModules: real, virtualModules: virtual };
  }, [modules, virtualModuleIdSet]);

  const realModuleIds = React.useMemo(
    () => new Set(realModules.map((m) => m.id)),
    [realModules],
  );

  // Add Module dialog state (shared with the Add Submodule dialog)
  const [showAddModule, setShowAddModule] = useState(false);
  const [moduleTitle, setModuleTitle] = useState("");
  const [moduleDescription, setModuleDescription] = useState("");
  const [moduleSlug, setModuleSlug] = useState("");
  const [moduleAutoSlug, setModuleAutoSlug] = useState(true);

  // Add Lesson dialog state
  const [showAddLesson, setShowAddLesson] = useState(false);
  const [lessonParentId, setLessonParentId] = useState("");
  const [lessonTitle, setLessonTitle] = useState("");
  const [lessonSlug, setLessonSlug] = useState("");
  const [lessonAutoSlug, setLessonAutoSlug] = useState(true);
  const [lessonType, setLessonType] =
    useState<LearningCoursesProgramContentType>("Lesson");
  const [lessonFormat, setLessonFormat] = useState<LessonContentFormat>(
    DEFAULT_LESSON_FORMAT,
  );

  // Delete confirmation state
  const [deleteTarget, setDeleteTarget] = useState<{
    id: string;
    title: string;
    isModule: boolean;
  } | null>(null);

  // Edit module dialog state
  const [editTarget, setEditTarget] = useState<ContentItem | null>(null);
  const [editTitle, setEditTitle] = useState("");
  const [editDescription, setEditDescription] = useState("");
  const [editSlug, setEditSlug] = useState("");
  const [editAutoSlug, setEditAutoSlug] = useState(true);

  const [error, setError] = useState("");

  // Submodule dialog state
  const [submoduleParentId, setSubmoduleParentId] = useState<string | null>(
    null,
  );

  // Active drag item — drives the root-level DragOverlay so the preview can
  // cross parent container boundaries (each module owns its own
  // SortableContext, which would otherwise trap the visual).
  const [activeItemId, setActiveItemId] = useState<string | null>(null);

  // DnD sensors
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
  );

  function handleDragStart(event: DragStartEvent) {
    setActiveItemId(String(event.active.id));
  }

  function handleDragEnd(event: DragEndEvent) {
    setActiveItemId(null);
    const { active, over } = event;
    if (!over) return;
    const activeId = String(active.id);
    const overId = String(over.id);
    if (activeId === overId) return;

    // Module reorder: only real modules are draggable, so the payload never
    // contains a virtual id.
    if (realModuleIds.has(activeId)) {
      if (!realModuleIds.has(overId)) return;
      const oldIndex = realModules.findIndex((m) => m.id === activeId);
      const newIndex = realModules.findIndex((m) => m.id === overId);
      const newIds = arrayMove(
        realModules.map((m) => m.id),
        oldIndex,
        newIndex,
      );
      setError("");
      startTransition(async () => {
        const result = await reorderContent(courseId, newIds);
        if (result.success) {
          router.refresh();
        } else {
          setError(result.error);
        }
      });
      return;
    }

    // Lesson drag (same-module reorder or cross-module move). Find source +
    // dest modules by walking allItems parentage. Virtual dest maps to
    // newParentId: null (top-level orphan).
    const sourceModule = modules.find((m) =>
      allItems.some((i) => i.id === activeId && i.parentId === m.id),
    );
    if (!sourceModule) return;

    const MODULE_DROP_PREFIX = "module-drop-";
    let destModule: ContentItem | undefined;
    if (overId.startsWith(MODULE_DROP_PREFIX)) {
      const moduleId = overId.slice(MODULE_DROP_PREFIX.length);
      destModule = modules.find((m) => m.id === moduleId);
    } else {
      destModule = modules.find((m) =>
        allItems.some((i) => i.id === overId && i.parentId === m.id),
      );
    }
    if (!destModule) return;

    const destChildren = allItems
      .filter((i) => i.parentId === destModule.id)
      .sort((a, b) => a.order - b.order);

    if (sourceModule.id === destModule.id) {
      const oldIndex = destChildren.findIndex((c) => c.id === activeId);
      const newIndex = destChildren.findIndex((c) => c.id === overId);
      if (oldIndex < 0 || newIndex < 0) return;
      const newIds = arrayMove(
        destChildren.map((c) => c.id),
        oldIndex,
        newIndex,
      );
      setError("");
      startTransition(async () => {
        const result = await reorderContent(courseId, newIds);
        if (result.success) {
          router.refresh();
        } else {
          setError(result.error);
        }
      });
      return;
    }

    const overIsDropArea = overId.startsWith(MODULE_DROP_PREFIX);
    const newSortOrder = overIsDropArea
      ? destChildren.length
      : destChildren.findIndex((c) => c.id === overId);
    const newParentId = isVirtualModule(destModule.id) ? null : destModule.id;
    setError("");
    startTransition(async () => {
      const result = await moveContent(
        courseId,
        activeId,
        newParentId,
        newSortOrder,
      );
      if (result.success) {
        router.refresh();
      } else {
        setError(result.error);
      }
    });
  }

  function resetModuleDialogState() {
    setModuleTitle("");
    setModuleDescription("");
    setModuleSlug("");
    setModuleAutoSlug(true);
  }

  function handleModuleTitleChange(value: string) {
    setModuleTitle(value);
    if (moduleAutoSlug) {
      setModuleSlug(slugify(value));
    }
  }

  function handleModuleSlugChange(value: string) {
    setModuleAutoSlug(false);
    setModuleSlug(slugify(value));
  }

  function handleLessonTitleChange(value: string) {
    setLessonTitle(value);
    if (lessonAutoSlug) {
      setLessonSlug(slugify(value));
    }
  }

  function handleLessonSlugChange(value: string) {
    setLessonAutoSlug(false);
    setLessonSlug(slugify(value));
  }

  function openAddSubmoduleDialog(parentId: string) {
    setSubmoduleParentId(parentId);
    resetModuleDialogState();
    setError("");
  }

  function handleAddSubmodule() {
    const parentId = submoduleParentId!;
    setError("");
    const parentChildren = allItems.filter(
      (i) => i.parentId === parentId,
    );
    startTransition(async () => {
      const result = await addContent({
        courseId,
        parentId,
        title: moduleTitle.trim(),
        description: moduleDescription.trim(),
        ...(normalizeSlug(moduleSlug) ? { slug: normalizeSlug(moduleSlug) } : {}),
        type: "Module",
        sortOrder: parentChildren.length,
      });
      if (result.success) {
        setSubmoduleParentId(null);
        resetModuleDialogState();
        router.refresh();
      } else {
        setError(result.error);
      }
    });
  }

  function toggleModule(id: string) {
    setOpenModules((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function handleAddModule() {
    setError("");
    startTransition(async () => {
      const result = await addContent({
        courseId,
        title: moduleTitle.trim(),
        description: moduleDescription.trim(),
        ...(normalizeSlug(moduleSlug) ? { slug: normalizeSlug(moduleSlug) } : {}),
        type: "Module",
        sortOrder: realModules.length,
      });
      if (result.success) {
        setShowAddModule(false);
        resetModuleDialogState();
        router.refresh();
      } else {
        setError(result.error);
      }
    });
  }

  function handleAddLesson() {
    setError("");
    const parentChildren = allItems.filter(
      (i) => i.parentId === lessonParentId,
    );
    startTransition(async () => {
      const result = await addContent({
        courseId,
        parentId: normalizeParentId(lessonParentId),
        title: lessonTitle.trim(),
        ...(normalizeSlug(lessonSlug) ? { slug: normalizeSlug(lessonSlug) } : {}),
        type: lessonType,
        ...(lessonType === "Lesson" ? { lessonFormat } : {}),
        sortOrder: parentChildren.length,
      });
      if (result.success) {
        setShowAddLesson(false);
        setLessonTitle("");
        setLessonSlug("");
        setLessonAutoSlug(true);
        setLessonType("Lesson");
        setLessonFormat(DEFAULT_LESSON_FORMAT);
        router.refresh();
      } else {
        setError(result.error);
      }
    });
  }

  function handleDelete() {
    const target = deleteTarget!;
    setError("");
    startTransition(async () => {
      const result = await deleteContent(courseId, target.id);
      if (result.success) {
        setDeleteTarget(null);
        router.refresh();
      } else {
        setError(result.error);
      }
    });
  }

  function openAddLessonDialog(parentId: string) {
    setLessonParentId(parentId);
    setLessonTitle("");
    setLessonSlug("");
    setLessonAutoSlug(true);
    setLessonType("Lesson" as LearningCoursesProgramContentType);
    setLessonFormat(DEFAULT_LESSON_FORMAT);
    setError("");
    setShowAddLesson(true);
  }

  function openEditModuleDialog(item: ContentItem) {
    setEditTarget(item);
    setEditTitle(item.title);
    setEditDescription(item.description ?? "");
    setEditSlug(item.slug);
    setEditAutoSlug(true);
    setError("");
  }

  function handleEditModule() {
    const target = editTarget!;
    setError("");
    startTransition(async () => {
      const result = await updateContent({
        courseId,
        contentId: target.id,
        title: editTitle.trim(),
        description: editDescription.trim(),
        slug: normalizeSlug(editSlug) || normalizeSlug(editTitle),
      });
      if (result.success) {
        setEditTarget(null);
        router.refresh();
      } else {
        setError(result.error);
      }
    });
  }

  function handleDuplicate(item: ContentItem) {
    setError("");
    startTransition(async () => {
      const result = await addContent({
        courseId,
        parentId: normalizeParentId(item.parentId),
        title: `${item.title} (copy)`,
        description: item.description ?? undefined,
        type: item.type,
      });
      if (result.success) {
        router.refresh();
      } else {
        setError(result.error);
      }
    });
  }

  function handleMoveModule(moduleId: string, direction: "up" | "down") {
    const ids = realModules.map((m) => m.id);
    const idx = ids.indexOf(moduleId);
    const swapIdx = direction === "up" ? idx - 1 : idx + 1;
    // Boundary actions are disabled in the rendered module controls.
    [ids[idx], ids[swapIdx]] = [ids[swapIdx], ids[idx]];
    setError("");
    startTransition(async () => {
      const result = await reorderContent(courseId, ids);
      if (result.success) {
        router.refresh();
      } else {
        setError(result.error);
      }
    });
  }

  function handleMoveLesson(
    parentId: string,
    itemId: string,
    direction: "up" | "down",
  ) {
    const siblings = allItems
      .filter((i) => i.parentId === parentId)
      .sort((a, b) => a.order - b.order);
    const ids = siblings.map((s) => s.id);
    const idx = ids.indexOf(itemId);
    const swapIdx = direction === "up" ? idx - 1 : idx + 1;
    // Boundary actions are disabled in the rendered lesson controls.
    [ids[idx], ids[swapIdx]] = [ids[swapIdx], ids[idx]];
    setError("");
    startTransition(async () => {
      const result = await reorderContent(courseId, ids);
      if (result.success) {
        router.refresh();
      } else {
        setError(result.error);
      }
    });
  }

  function renderDragPreview(itemId: string) {
    const item = allItems.find((i) => i.id === itemId) ??
      modules.find((m) => m.id === itemId);
    if (!item) return null;
    const config = typeConfig[item.type] ?? {
      icon: FileText,
      label: item.type,
    };
    const Icon = config.icon;
    return (
      <div className="flex items-center gap-3 rounded-lg border bg-card px-4 py-3 shadow-2xl rotate-1 cursor-grabbing">
        <Icon className="size-4 text-primary" />
        <span className="text-sm font-medium">{item.title}</span>
        <Badge variant="outline" className="text-xs capitalize">
          {config.label}
        </Badge>
      </div>
    );
  }

  const renderModuleCard = (
    module: ContentItem,
    displayIndex: number,
    moduleIsVirtual: boolean,
    sortableProps?: {
      listeners: ReturnType<typeof useSortable>["listeners"];
      isDragging: boolean;
    },
  ) => {
    const children = allItems
      .filter((i) => i.parentId === module.id)
      .sort((a, b) => a.order - b.order);
    const isOpen = openModules.has(module.id);
    const realModuleIndex = realModules.findIndex((m) => m.id === module.id);
    const moveDownDisabled =
      isPending ||
      realModuleIndex < 0 ||
      realModuleIndex === realModules.length - 1;

    return (
      <Collapsible
        open={isOpen}
        onOpenChange={() => toggleModule(module.id)}
      >
        <Card className={sortableProps?.isDragging ? "opacity-50" : ""}>
          <CardHeader className="flex flex-row items-center gap-3 pb-3">
            {sortableProps?.listeners && (
              <button
                type="button"
                className="cursor-grab touch-none"
                {...sortableProps.listeners}
              >
                <GripVertical className="size-5 text-muted-foreground" />
              </button>
            )}
            <CollapsibleTrigger
              render={<Button variant="ghost" size="icon" className="size-8" />}
            >
              {isOpen ? (
                <ChevronDown className="size-4" />
              ) : (
                <ChevronRight className="size-4" />
              )}
            </CollapsibleTrigger>
            <div className="flex size-8 items-center justify-center rounded-lg bg-primary/10 text-sm font-bold text-primary">
              {displayIndex + 1}
            </div>
            <div className="flex-1">
              <CardTitle className="text-base">
                {module.title}
              </CardTitle>
              {module.description && (
                <CardDescription className="mt-0.5 text-xs">
                  {module.description}
                </CardDescription>
              )}
            </div>
            <div className="flex items-center gap-2">
              <Badge
                variant={
                  visibilityVariant[module.visibility] ?? "outline"
                }
              >
                {module.visibility}
              </Badge>
              <span className="text-xs text-muted-foreground">
                {children.length} items
              </span>
              {!moduleIsVirtual && (
                <div className="flex items-center gap-1">
                  <ContentActionButton
                    label="Edit module"
                    icon={Edit}
                    onClick={() => openEditModuleDialog(module)}
                  />
                  <ContentActionButton
                    label="Duplicate module"
                    icon={Copy}
                    onClick={() => handleDuplicate(module)}
                    disabled={isPending}
                  />
                  <ContentActionButton
                    label="Add submodule"
                    icon={Plus}
                    onClick={() =>
                      openAddSubmoduleDialog(module.id)
                    }
                  />
                  <ContentActionButton
                    label="Move module up"
                    icon={ArrowUp}
                    onClick={() =>
                      handleMoveModule(module.id, "up")
                    }
                    disabled={isPending || realModuleIndex <= 0}
                  />
                  <ContentActionButton
                    label="Move module down"
                    icon={ArrowDown}
                    onClick={() =>
                      handleMoveModule(module.id, "down")
                    }
                    disabled={moveDownDisabled}
                  />
                  <ContentActionButton
                    label="Delete module"
                    icon={Trash2}
                    onClick={() =>
                      setDeleteTarget({
                        id: module.id,
                        title: module.title,
                        isModule: true,
                      })
                    }
                    destructive
                  />
                </div>
              )}
            </div>
          </CardHeader>
          <CollapsibleContent>
            <DroppableCardArea moduleId={module.id}>
              <CardContent className="pt-0">
                {children.length === 0 ? (
                  <p className="py-4 text-center text-sm text-muted-foreground">
                    No content items yet
                  </p>
                ) : (
                  <SortableContext
                    items={children.map((c) => c.id)}
                    strategy={verticalListSortingStrategy}
                  >
                    <div className="divide-y rounded-lg border">
                      {children.map((item, itemIndex) => {
                        const subchildren = allItems
                          .filter((i) => i.parentId === item.id)
                          .sort((a, b) => a.order - b.order);
                        const isSubmodule =
                          subchildren.length > 0;
                        const config = typeConfig[
                          item.type
                        ] ?? {
                          icon: FileText,
                          label: item.type,
                        };
                        const Icon = config.icon;
                        return (
                          <SortableItem
                            key={item.id}
                            id={item.id}
                          >
                            {({
                              ref: itemRef,
                              style: itemStyle,
                              listeners: itemListeners,
                              isDragging: itemDragging,
                            }) => (
                              <div
                                ref={itemRef}
                                style={itemStyle}
                              >
                                <div
                                  className={`group flex items-center gap-3 px-4 py-3 transition-colors hover:bg-muted/50 ${itemDragging ? "opacity-50" : ""}`}
                                >
                                  <button
                                    type="button"
                                    className="cursor-grab touch-none"
                                    {...itemListeners}
                                  >
                                    <GripVertical className="size-4 text-muted-foreground/50" />
                                  </button>
                                  <div className="flex size-8 items-center justify-center rounded bg-muted">
                                    <Icon className="size-4 text-muted-foreground" />
                                  </div>
                                  <div className="flex-1">
                                    <p className="text-sm font-medium">
                                      {item.title}
                                    </p>
                                    {isSubmodule && (
                                      <p className="text-xs text-muted-foreground">
                                        {subchildren.length}{" "}
                                        sub-items
                                      </p>
                                    )}
                                  </div>
                                  <Badge
                                    variant="outline"
                                    className="text-xs capitalize"
                                  >
                                    {config.label}
                                  </Badge>
                                  <Badge
                                    variant={
                                      visibilityVariant[
                                        item.visibility
                                      ] ?? "outline"
                                    }
                                    className="text-xs"
                                  >
                                    {item.visibility}
                                  </Badge>
                                  {item.duration != null &&
                                    item.duration > 0 && (
                                      <span className="flex items-center gap-1 text-xs text-muted-foreground">
                                        <Clock className="size-3" />
                                        {item.duration}m
                                      </span>
                                    )}
                                  <div className="flex items-center gap-1">
                                    <ContentActionButton
                                      label={`Edit ${config.label}`}
                                      icon={Edit}
                                      onClick={() =>
                                        navigateToContentItem(
                                          item.slug || item.id,
                                        )
                                      }
                                      className="size-7"
                                    />
                                    <ContentActionButton
                                      label="Duplicate"
                                      icon={Copy}
                                      onClick={() =>
                                        handleDuplicate(item)
                                      }
                                      disabled={isPending}
                                      className="size-7"
                                    />
                                    <ContentActionButton
                                      label="Move up"
                                      icon={ArrowUp}
                                      onClick={() =>
                                        handleMoveLesson(
                                          module.id,
                                          item.id,
                                          "up",
                                        )
                                      }
                                      disabled={
                                        isPending ||
                                        itemIndex === 0
                                      }
                                      className="size-7"
                                    />
                                    <ContentActionButton
                                      label="Move down"
                                      icon={ArrowDown}
                                      onClick={() =>
                                        handleMoveLesson(
                                          module.id,
                                          item.id,
                                          "down",
                                        )
                                      }
                                      disabled={
                                        isPending ||
                                        itemIndex ===
                                          children.length - 1
                                      }
                                      className="size-7"
                                    />
                                    <ContentActionButton
                                      label="Delete"
                                      icon={Trash2}
                                      onClick={() =>
                                        setDeleteTarget({
                                          id: item.id,
                                          title: item.title,
                                          isModule: false,
                                        })
                                      }
                                      destructive
                                      className="size-7"
                                    />
                                  </div>
                                </div>
                                {isSubmodule && (
                                  <div className="ml-8 border-l pl-4 pb-2">
                                    {subchildren.map((sub) => {
                                      const subConfig =
                                        typeConfig[
                                          sub.type
                                        ] ?? {
                                          icon: FileText,
                                          label: sub.type,
                                        };
                                      const SubIcon =
                                        subConfig.icon;
                                      return (
                                        <div
                                          key={sub.id}
                                          className="group flex items-center gap-3 px-4 py-2 transition-colors hover:bg-muted/30"
                                        >
                                          <div className="flex size-6 items-center justify-center rounded bg-muted">
                                            <SubIcon className="size-3 text-muted-foreground" />
                                          </div>
                                          <div className="flex-1">
                                            <p className="text-sm">
                                              {sub.title}
                                            </p>
                                          </div>
                                          <Badge
                                            variant="outline"
                                            className="text-xs"
                                          >
                                            {subConfig.label}
                                          </Badge>
                                          <Badge
                                            variant={
                                              visibilityVariant[
                                                sub.visibility
                                              ] ?? "outline"
                                            }
                                            className="text-xs"
                                          >
                                            {sub.visibility}
                                          </Badge>
                                          <div className="flex items-center gap-1">
                                            <ContentActionButton
                                              label="Edit"
                                              icon={Edit}
                                              onClick={() =>
                                                navigateToContentItem(
                                                  sub.slug || sub.id,
                                                )
                                              }
                                              className="size-6"
                                            />
                                            <ContentActionButton
                                              label="Delete"
                                              icon={Trash2}
                                              onClick={() =>
                                                setDeleteTarget(
                                                  {
                                                    id: sub.id,
                                                    title:
                                                      sub.title,
                                                    isModule: false,
                                                  },
                                                )
                                              }
                                              destructive
                                              className="size-6"
                                            />
                                          </div>
                                        </div>
                                      );
                                    })}
                                    <Button
                                      variant="ghost"
                                      size="sm"
                                      className="mt-1 w-full text-xs text-muted-foreground"
                                      onClick={() =>
                                        openAddLessonDialog(
                                          item.id,
                                        )
                                      }
                                    >
                                      <Plus className="mr-1 size-3" />
                                      Add to {item.title}
                                    </Button>
                                  </div>
                                )}
                              </div>
                            )}
                          </SortableItem>
                        );
                      })}
                    </div>
                  </SortableContext>
                )}
                <Button
                  variant="ghost"
                  size="sm"
                  className="mt-2 w-full text-muted-foreground"
                  onClick={() => openAddLessonDialog(module.id)}
                >
                  <Plus className="mr-2 size-4" />
                  {moduleIsVirtual
                    ? "Add Content Item"
                    : "Add Lesson"}
                </Button>
              </CardContent>
            </DroppableCardArea>
          </CollapsibleContent>
        </Card>
      </Collapsible>
    );
  };

  return (
    <>
      <DndContext
        sensors={sensors}
        collisionDetection={closestCorners}
        onDragStart={handleDragStart}
        onDragEnd={handleDragEnd}
        onDragCancel={() => setActiveItemId(null)}
      >
        <div className="space-y-4">
          <SortableContext
            items={realModules.map((m) => m.id)}
            strategy={verticalListSortingStrategy}
          >
            {realModules.map((module, index) => (
              <SortableItem key={module.id} id={module.id}>
                {({ ref, style, listeners, isDragging }) => (
                  <div ref={ref} style={style}>
                    {renderModuleCard(module, index, false, {
                      listeners,
                      isDragging,
                    })}
                  </div>
                )}
              </SortableItem>
            ))}
          </SortableContext>

          {virtualModules.map((module, vIndex) => (
            <div key={module.id}>
              {renderModuleCard(
                module,
                realModules.length + vIndex,
                true,
              )}
            </div>
          ))}

          {/* Add Module button at the bottom */}
          <Button
            variant="outline"
            className="w-full border-dashed"
            onClick={() => {
              resetModuleDialogState();
              setError("");
              setShowAddModule(true);
            }}
          >
            <Plus className="mr-2 size-4" />
            Add Module
          </Button>
        </div>

        <DragOverlay dropAnimation={null}>
          {activeItemId ? renderDragPreview(activeItemId) : null}
        </DragOverlay>
      </DndContext>

      <Dialog open={showAddModule} onOpenChange={setShowAddModule}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add Module</DialogTitle>
            <DialogDescription>
              Create a new module to organize your course content.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-4 py-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="module-title">Title</Label>
              <Input
                id="module-title"
                placeholder="e.g. Introduction to Game Design"
                value={moduleTitle}
                onChange={(e) => handleModuleTitleChange(e.target.value)}
                autoFocus
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="module-slug">URL Slug (optional)</Label>
              <Input
                id="module-slug"
                placeholder="introduction-to-game-design"
                value={moduleSlug}
                onChange={(e) => handleModuleSlugChange(e.target.value)}
                onBlur={() => setModuleSlug(normalizeSlug(moduleSlug))}
              />
              <p className="text-muted-foreground text-xs">
                Auto-generated from title. Edit to customize.
              </p>
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="module-desc">Description (optional)</Label>
              <Input
                id="module-desc"
                placeholder="Brief description of this module"
                value={moduleDescription}
                onChange={(e) => setModuleDescription(e.target.value)}
              />
            </div>
            {error && <p className="text-sm text-destructive">{error}</p>}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowAddModule(false)}>
              Cancel
            </Button>
            <Button
              onClick={handleAddModule}
              disabled={!moduleTitle.trim() || isPending}
            >
              {isPending && <Loader2 className="mr-2 size-4 animate-spin" />}
              Add Module
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Add Lesson Dialog ── */}
      <Dialog open={showAddLesson} onOpenChange={setShowAddLesson}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add Lesson</DialogTitle>
            <DialogDescription>
              Add a lesson or course activity to this module.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-4 py-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="lesson-title">Title</Label>
              <Input
                id="lesson-title"
                placeholder="e.g. Setting Up Your Environment"
                value={lessonTitle}
                onChange={(e) => handleLessonTitleChange(e.target.value)}
                autoFocus
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="lesson-slug">URL Slug (optional)</Label>
              <Input
                id="lesson-slug"
                placeholder="setting-up-your-environment"
                value={lessonSlug}
                onChange={(e) => handleLessonSlugChange(e.target.value)}
                onBlur={() => setLessonSlug(normalizeSlug(lessonSlug))}
              />
              <p className="text-muted-foreground text-xs">
                Auto-generated from title. Edit to customize.
              </p>
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="lesson-type">Type</Label>
              <Select
                value={lessonType}
                onValueChange={(v) =>
                  setLessonType(v as LearningCoursesProgramContentType)
                }
              >
                <SelectTrigger id="lesson-type">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {lessonTypes.map((t) => (
                    <SelectItem key={t.value} value={t.value}>
                      {t.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {lessonType === "Lesson" && (
              <div className="flex flex-col gap-2">
                <Label htmlFor="lesson-format">Lesson format</Label>
                <Select
                  value={lessonFormat}
                  onValueChange={(value) =>
                    setLessonFormat(value as LessonContentFormat)
                  }
                >
                  <SelectTrigger id="lesson-format">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {LESSON_FORMATS.map((format) => (
                      <SelectItem key={format.value} value={format.value}>
                        {format.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            {error && <p className="text-sm text-destructive">{error}</p>}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowAddLesson(false)}>
              Cancel
            </Button>
            <Button
              onClick={handleAddLesson}
              disabled={!lessonTitle.trim() || isPending}
            >
              {isPending && <Loader2 className="mr-2 size-4 animate-spin" />}
              Add Lesson
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Delete Confirmation Dialog ── */}
      <Dialog
        open={!!deleteTarget}
        onOpenChange={() => setDeleteTarget(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              Delete {deleteTarget?.isModule ? "Module" : "Item"}
            </DialogTitle>
            <DialogDescription>
              Are you sure you want to delete &ldquo;{deleteTarget?.title}
              &rdquo;?
              {deleteTarget?.isModule &&
                " All lessons within this module will also be deleted."}{" "}
              This action cannot be undone.
            </DialogDescription>
          </DialogHeader>
          {error && <p className="text-sm text-destructive">{error}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteTarget(null)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={handleDelete}
              disabled={isPending}
            >
              {isPending && <Loader2 className="mr-2 size-4 animate-spin" />}
              Delete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Edit Module Dialog ── */}
      <Dialog
        open={!!editTarget}
        onOpenChange={() => setEditTarget(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Edit Module</DialogTitle>
            <DialogDescription>
              Update the module title and description.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-4 py-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="edit-module-title">Title</Label>
              <Input
                id="edit-module-title"
                value={editTitle}
                onChange={(e) => {
                  setEditTitle(e.target.value);
                  if (editAutoSlug) {
                    setEditSlug(slugify(e.target.value));
                  }
                }}
                autoFocus
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="edit-module-slug">URL Slug</Label>
              <Input
                id="edit-module-slug"
                value={editSlug}
                onChange={(e) => {
                  setEditAutoSlug(false);
                  setEditSlug(slugify(e.target.value));
                }}
                onBlur={() => setEditSlug(normalizeSlug(editSlug))}
              />
              <p className="text-muted-foreground text-xs">
                Auto-generated from title. Edit to customize.
              </p>
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="edit-module-desc">Description (optional)</Label>
              <Input
                id="edit-module-desc"
                value={editDescription}
                onChange={(e) => setEditDescription(e.target.value)}
              />
            </div>
            {error && <p className="text-sm text-destructive">{error}</p>}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditTarget(null)}>
              Cancel
            </Button>
            <Button
              onClick={handleEditModule}
              disabled={!editTitle.trim() || isPending}
            >
              {isPending && <Loader2 className="mr-2 size-4 animate-spin" />}
              Save Changes
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Add Submodule Dialog ── */}
      <Dialog
        open={submoduleParentId !== null}
        onOpenChange={() => setSubmoduleParentId(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add Submodule</DialogTitle>
            <DialogDescription>
              Create a submodule to further organize content within this module.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-4 py-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="submodule-title">Title</Label>
              <Input
                id="submodule-title"
                placeholder="e.g. Part A: Fundamentals"
                value={moduleTitle}
                onChange={(e) => handleModuleTitleChange(e.target.value)}
                autoFocus
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="submodule-slug">URL Slug (optional)</Label>
              <Input
                id="submodule-slug"
                placeholder="part-a-fundamentals"
                value={moduleSlug}
                onChange={(e) => handleModuleSlugChange(e.target.value)}
                onBlur={() => setModuleSlug(normalizeSlug(moduleSlug))}
              />
              <p className="text-muted-foreground text-xs">
                Auto-generated from title. Edit to customize.
              </p>
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="submodule-desc">Description (optional)</Label>
              <Input
                id="submodule-desc"
                placeholder="Brief description of this submodule"
                value={moduleDescription}
                onChange={(e) => setModuleDescription(e.target.value)}
              />
            </div>
            {error && <p className="text-sm text-destructive">{error}</p>}
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setSubmoduleParentId(null)}
            >
              Cancel
            </Button>
            <Button
              onClick={handleAddSubmodule}
              disabled={!moduleTitle.trim() || isPending}
            >
              {isPending && <Loader2 className="mr-2 size-4 animate-spin" />}
              Add Submodule
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
