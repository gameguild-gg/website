'use client';

import { PeriodType } from '@/components/common/filters/filter-context';
import { PeriodSelector } from '@/components/common/filters/period-selector';
import { ViewModeToggle } from '@/components/common/filters/view-mode-toggle';
import type { CourseLevel, CourseStatus } from '@/components/courses/common/course-taxonomy';
import { ProgramCategory } from '@/lib/api/generated';
import { CourseAreaFilter } from './course-area-filter';
import { CourseLevelFilter } from './course-level-filter';
import { CourseSearchBar } from './course-search-bar';
import { CourseStatusFilter } from './course-status-filter';

// Type aliases to maintain existing naming
type CourseArea = ProgramCategory;

interface CourseFilterControlsProps {
  searchTerm: string;
  onSearchChange: (value: string) => void;
  selectedStatuses: CourseStatus[];
  onToggleStatus: (status: CourseStatus) => void;
  selectedAreas: CourseArea[];
  onToggleArea: (area: CourseArea) => void;
  selectedLevels: CourseLevel[];
  onToggleLevel: (level: CourseLevel) => void;
  selectedPeriod: PeriodType;
  onPeriodChange: (period: PeriodType) => void;
  viewMode: 'cards' | 'row' | 'table';
  onViewModeChange: (mode: 'cards' | 'row' | 'table') => void;
  hideViewToggle?: boolean;
}

export function CourseFilterControls({
  searchTerm,
  onSearchChange,
  selectedStatuses,
  onToggleStatus,
  selectedAreas,
  onToggleArea,
  selectedLevels,
  onToggleLevel,
  selectedPeriod,
  onPeriodChange,
  viewMode,
  onViewModeChange,
  hideViewToggle = false,
}: CourseFilterControlsProps) {
  return (
    <div className="space-y-4">
      {/* Desktop Layout (xl and up) - Single Row */}
      <div className="hidden xl:flex xl:items-center xl:justify-between xl:gap-6">
        {/* Left Side - Search Bar */}
        <div className="flex-1 max-w-md">
          <CourseSearchBar searchTerm={searchTerm} onSearchChange={onSearchChange} />
        </div>

        {/* Center - Filter Dropdowns */}
        <div className="flex gap-3 items-center">
          <CourseStatusFilter selectedStatuses={selectedStatuses} onToggleStatus={onToggleStatus} />
          <CourseAreaFilter selectedAreas={selectedAreas} onToggleArea={onToggleArea} />
          <CourseLevelFilter selectedLevels={selectedLevels} onToggleLevel={onToggleLevel} />
        </div>

        {/* Right Side - Period Selector and View Mode Toggle */}
        <div className="flex items-center gap-4">
          <PeriodSelector selectedPeriod={selectedPeriod} onPeriodChange={onPeriodChange} />
          {!hideViewToggle && <ViewModeToggle viewMode={viewMode} onViewModeChange={onViewModeChange} />}
        </div>
      </div>

      {/* Tablet/Mobile Layout (lg and below) - Two Rows */}
      <div className="xl:hidden space-y-4">
        {/* First Row - Period Selector and View Mode Toggle */}
        <div className="flex items-center justify-between gap-4">
          <div className={hideViewToggle ? 'flex-1' : 'flex-1 max-w-xs'}>
            <PeriodSelector selectedPeriod={selectedPeriod} onPeriodChange={onPeriodChange} />
          </div>
          {!hideViewToggle && <ViewModeToggle viewMode={viewMode} onViewModeChange={onViewModeChange} />}
        </div>

        {/* Second Row - Search and Dropdown Filters */}
        <div className="flex items-center gap-4">
          {/* Search Bar - Full width on mobile, flex-1 on sm+ */}
          <div className="flex-1 min-w-0">
            <CourseSearchBar searchTerm={searchTerm} onSearchChange={onSearchChange} />
          </div>

          {/* Filter Dropdowns */}
          <div className="flex gap-3 items-center flex-shrink-0">
            <CourseStatusFilter selectedStatuses={selectedStatuses} onToggleStatus={onToggleStatus} />
            <CourseAreaFilter selectedAreas={selectedAreas} onToggleArea={onToggleArea} />
            <CourseLevelFilter selectedLevels={selectedLevels} onToggleLevel={onToggleLevel} />
          </div>
        </div>
      </div>
    </div>
  );
}
