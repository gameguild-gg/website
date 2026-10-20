/**
 * Filter options constants
 */

import type { FilterConfig } from '../types'
import type { SelectOption } from './filter-select'

export const STORAGE_TYPE_OPTIONS = [
  { value: 'all', label: 'All types' },
  { value: 'local', label: 'Local' },
  { value: 'gameguild-cloud', label: 'GameGuild Cloud' },
  { value: 'google-drive', label: 'Google Drive' },
] as const satisfies readonly SelectOption<NonNullable<FilterConfig['storageType']>>[]

export const MIME_TYPE_OPTIONS = [
  { value: 'all', label: 'All types' },
  { value: 'image', label: 'Images' },
  { value: 'video', label: 'Videos' },
  { value: 'audio', label: 'Audio' },
  { value: 'text', label: 'Text' },
]

export const ASSET_TYPE_OPTIONS = [
  { value: 'all', label: 'All' },
  { value: 'standard', label: 'Standard' },
  { value: 'bundler', label: 'Bundler' },
] as const satisfies readonly SelectOption<NonNullable<FilterConfig['assetType']>>[]

export const USAGE_FILTER_OPTIONS = [
  { value: 'all', label: 'All' },
  { value: 'used', label: 'Used' },
  { value: 'unused', label: 'Unused' },
] as const satisfies readonly SelectOption<NonNullable<FilterConfig['usageFilter']>>[]

export const ITEMS_PER_PAGE_OPTIONS = [
  { value: '12', label: '12 per page' },
  { value: '24', label: '24 per page' },
  { value: '48', label: '48 per page' },
  { value: '96', label: '96 per page' },
]
