import type { ProgramContent } from '@/lib/api/generated';

// Week 13 - Time Series & Search Engines

export const week13TimeseriesSearchContent: ProgramContent = {
  id: 'databases-week13',
  programId: 'databases-program-1',
  slug: 'week13-timeseries-search',
  parentId: undefined,
  title: 'Week 13 - Time Series & Search Engines',
  description: 'TimescaleDB (time-series databases), Elasticsearch (search engines), inverted indices, aggregations',
  type: 0, // Page
  body: '# Week 13 - Time Series & Search Engines\n\nTimescaleDB and Elasticsearch fundamentals.',
  sortOrder: 13,
  isRequired: true,
  estimatedMinutes: 360,
  visibility: 1, // Published
  program: undefined,
  parent: undefined,
  children: [],
  contentInteractions: [],
  createdAt: '2026-04-06T00:00:00Z',
  updatedAt: '2026-04-06T00:00:00Z',
};

export const week13TimescaleDBContent: ProgramContent = {
  id: 'databases-week13-timescaledb',
  programId: 'databases-program-1',
  slug: 'timescaledb-fundamentals',
  parentId: 'databases-week13',
  title: 'TimescaleDB Fundamentals',
  description: 'Time-series database built on PostgreSQL: hypertables, compression, retention policies, continuous aggregates',
  type: 0, // Page
  body: '',
  sortOrder: 1,
  isRequired: true,
  estimatedMinutes: 120,
  visibility: 1, // Published
  program: undefined,
  parent: week13TimeseriesSearchContent,
  children: [],
  contentInteractions: [],
  createdAt: '2026-04-06T00:00:00Z',
  updatedAt: '2026-04-06T00:00:00Z',
};

export const week13ElasticsearchContent: ProgramContent = {
  id: 'databases-week13-elasticsearch',
  programId: 'databases-program-1',
  slug: 'elasticsearch-fundamentals',
  parentId: 'databases-week13',
  title: 'Elasticsearch Fundamentals',
  description: 'Search engine for full-text search, log analysis, and real-time analytics',
  type: 0, // Page
  body: '',
  sortOrder: 2,
  isRequired: true,
  estimatedMinutes: 120,
  visibility: 1, // Published
  program: undefined,
  parent: week13TimeseriesSearchContent,
  children: [],
  contentInteractions: [],
  createdAt: '2026-04-06T00:00:00Z',
  updatedAt: '2026-04-06T00:00:00Z',
};

export const week13QuizContent: ProgramContent = {
  id: 'databases-week13-quiz',
  programId: 'databases-program-1',
  slug: 'quiz11',
  parentId: 'databases-week13',
  title: 'Quiz 11 - Time Series & Search Engines',
  description: '12 questions covering TimescaleDB (hypertables, compression, continuous aggregates) and Elasticsearch (inverted indices, query DSL, aggregations)',
  type: 2, // Quiz
  body: '',
  sortOrder: 3,
  isRequired: true,
  estimatedMinutes: 30,
  visibility: 1, // Published
  program: undefined,
  parent: week13TimeseriesSearchContent,
  children: [],
  contentInteractions: [],
  createdAt: '2026-04-06T00:00:00Z',
  updatedAt: '2026-04-06T00:00:00Z',
};

export const week13ReadingsContent: ProgramContent = {
  id: 'databases-week13-readings',
  programId: 'databases-program-1',
  slug: 'readings-13',
  parentId: 'databases-week13',
  title: 'Week 13 Readings',
  description: 'Curated resources for TimescaleDB and Elasticsearch: official docs, tutorials, tools, videos, books, practice datasets',
  type: 0, // Page
  body: '',
  sortOrder: 4,
  isRequired: false,
  estimatedMinutes: 180,
  visibility: 1, // Published
  program: undefined,
  parent: week13TimeseriesSearchContent,
  children: [],
  contentInteractions: [],
  createdAt: '2026-04-06T00:00:00Z',
  updatedAt: '2026-04-06T00:00:00Z',
};

// Set up parent-child relationships
week13TimeseriesSearchContent.children = [
  week13TimescaleDBContent,
  week13ElasticsearchContent,
  week13QuizContent,
  week13ReadingsContent,
];

export default week13TimeseriesSearchContent;
