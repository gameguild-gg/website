'use client';

import { MarkdownRenderer } from '@game-guild/content-rendering';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Card, CardContent } from '@game-guild/ui/components/card';
import { BookOpen, CheckCircle, Clock } from 'lucide-react';
import { useState } from 'react';

interface ContentItem {
  id: string;
  title: string;
  type: 'lesson' | 'activity' | 'quiz' | 'assignment' | 'peer-review';
  status: 'locked' | 'available' | 'in-progress' | 'completed';
  duration?: number;
  description?: string;
  order: number;
  isRequired: boolean;
  content?: unknown;
}

interface LessonViewerProps {
  item: ContentItem;
  onComplete: () => void;
}

export function LessonViewer({ item, onComplete }: LessonViewerProps) {
  const [hasStarted, setHasStarted] = useState(false);
  const [isCompleting, setIsCompleting] = useState(false);

  const handleStart = () => {
    setHasStarted(true);
  };

  const handleComplete = async () => {
    setIsCompleting(true);

    // Simulate reading time
    setTimeout(() => {
      onComplete();
      setIsCompleting(false);
    }, 1000);
  };

  const lessonContent = {
    'lesson-1': {
      title: 'What is Game Development?',
      sections: [
        {
          title: 'Introduction',
          content: `Game development is the process of creating video games, from initial concept to final release. It involves multiple disciplines including programming, art, design, audio, and project management.`,
        },
        {
          title: 'Key Roles in Game Development',
          content: `
            <ul class="list-disc pl-6 space-y-2">
              <li><strong>Game Designer:</strong> Creates the game concept, mechanics, and rules</li>
              <li><strong>Programmer:</strong> Implements the game logic and systems</li>
              <li><strong>Artist:</strong> Creates visual assets like characters, environments, and UI</li>
              <li><strong>Audio Designer:</strong> Creates sound effects and music</li>
              <li><strong>Producer:</strong> Manages the project timeline and team coordination</li>
            </ul>
          `,
        },
        {
          title: 'Development Process',
          content: `The game development process typically follows these phases:
            <ol class="list-decimal pl-6 space-y-2 mt-4">
              <li><strong>Concept:</strong> Initial idea and vision</li>
              <li><strong>Pre-production:</strong> Prototyping and planning</li>
              <li><strong>Production:</strong> Full development of the game</li>
              <li><strong>Testing:</strong> Bug fixing and balancing</li>
              <li><strong>Release:</strong> Publishing and distribution</li>
            </ol>
          `,
        },
      ],
    },
    'lesson-2': {
      title: 'Game Programming Basics',
      sections: [
        {
          title: 'Programming Fundamentals',
          content: `Before diving into game programming, you need to understand basic programming concepts like variables, functions, loops, and conditions.`,
        },
        {
          title: 'Game Loop',
          content: `Every game runs on a main loop that continuously updates the game state and renders the graphics. The basic structure is:
            <pre class="bg-gray-900 p-4 rounded mt-4">
while (game is running) {
  processInput();
  updateGame();
  renderGraphics();
}
            </pre>
          `,
        },
        {
          title: 'Common Game Programming Patterns',
          content: `
            <ul class="list-disc pl-6 space-y-2">
              <li><strong>State Pattern:</strong> Managing different game states (menu, playing, paused)</li>
              <li><strong>Observer Pattern:</strong> Event systems for game interactions</li>
              <li><strong>Component System:</strong> Modular approach to game objects</li>
              <li><strong>Object Pooling:</strong> Efficient memory management for frequently created objects</li>
            </ul>
          `,
        },
      ],
    },
    'lesson-3': {
      title: 'Game Mechanics and Systems',
      sections: [
        {
          title: 'What are Game Mechanics?',
          content: `Game mechanics are the rules and systems that define how players interact with your game. They create the core gameplay experience.`,
        },
        {
          title: 'Types of Game Mechanics',
          content: `
            <ul class="list-disc pl-6 space-y-2">
              <li><strong>Movement:</strong> How players navigate the game world</li>
              <li><strong>Combat:</strong> Fighting and conflict resolution systems</li>
              <li><strong>Progression:</strong> Character or player advancement</li>
              <li><strong>Resource Management:</strong> Collecting and spending game resources</li>
              <li><strong>Puzzle Solving:</strong> Mental challenges and problem-solving</li>
            </ul>
          `,
        },
      ],
    },
  };

  const contentFromBody = (() => {
    if (typeof item.content === 'string' && item.content.trim().length > 0) {
      return {
        title: item.title,
        sections: [{ title: 'Lesson', content: item.content }],
      };
    }

    if (!item.content || typeof item.content !== 'object') {
      return null;
    }

    const body = item.content as Record<string, unknown>;
    if (Array.isArray(body.sections)) {
      const sections = body.sections
        .filter((section): section is { title?: unknown; content?: unknown } => typeof section === 'object' && section !== null)
        .map((section, index) => ({
          title: typeof section.title === 'string' ? section.title : `Section ${index + 1}`,
          content: typeof section.content === 'string' ? section.content : '',
        }))
        .filter((section) => section.content.trim().length > 0);

      if (sections.length > 0) {
        return {
          title: typeof body.title === 'string' ? body.title : item.title,
          sections,
        };
      }
    }

    const html = [body.html, body.content, body.body, body.markdown, body.text].find(
      (value): value is string => typeof value === 'string' && value.trim().length > 0,
    );

    if (html) {
      return {
        title: typeof body.title === 'string' ? body.title : item.title,
        sections: [{ title: 'Lesson', content: html }],
      };
    }

    return null;
  })();

  const content = contentFromBody ?? lessonContent[item.id as keyof typeof lessonContent] ?? {
    title: item.title,
    sections: [
      {
        title: 'Content',
        content: '<p>This lesson is available, but no formatted body was provided yet.</p>',
      },
    ],
  };

  if (!hasStarted) {
    return (
      <div className="text-center py-12">
        <BookOpen className="h-16 w-16 text-gray-400 mx-auto mb-4" />
        <h3 className="text-xl font-semibold mb-2">{item.title}</h3>
        <p className="text-gray-400 mb-6 max-w-md mx-auto">{item.description || 'Ready to start this lesson?'}</p>
        <div className="flex items-center justify-center gap-4 mb-6">
          {item.duration && (
            <div className="flex items-center gap-1 text-sm text-gray-400">
              <Clock className="h-4 w-4" />
              {item.duration} minutes
            </div>
          )}
          {item.isRequired && <Badge variant="secondary">Required</Badge>}
        </div>
        <Button onClick={handleStart} className="bg-blue-600 hover:bg-blue-700">
          Start Lesson
        </Button>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto">
      <div className="space-y-8">
        {content.sections.map((section, index) => (
          <Card key={index} className="bg-gray-900 border-gray-700">
            <CardContent className="p-6">
              <h3 className="text-lg font-semibold mb-4 text-white">{section.title}</h3>
              <div className="prose prose-invert max-w-none text-gray-300 leading-relaxed">
                <MarkdownRenderer content={section.content} />
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="mt-8 p-6 bg-gray-800 rounded-lg border border-gray-700">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <CheckCircle className="h-5 w-5 text-green-500" />
            <span className="text-white font-medium">Ready to mark as complete?</span>
          </div>
          <Button onClick={handleComplete} disabled={isCompleting} className="bg-green-600 hover:bg-green-700">
            {isCompleting ? 'Completing...' : 'Mark Complete'}
          </Button>
        </div>
        <p className="text-gray-400 text-sm mt-2">Marking this lesson as complete will unlock the next content item.</p>
      </div>
    </div>
  );
}
