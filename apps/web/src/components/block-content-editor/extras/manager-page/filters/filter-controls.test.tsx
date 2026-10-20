import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { FilterConfig } from '../types'
import { FilterMimeTypes } from './filter-mime-types'
import { FilterSelect } from './filter-select'
import { ASSET_TYPE_OPTIONS, STORAGE_TYPE_OPTIONS, USAGE_FILTER_OPTIONS } from './filter-options'

const imageExtensions = ['.jpg', '.jpeg', '.png', '.gif', '.svg', '.webp', '.bmp', '.ico', '.tiff', '.psd']

describe('manager filter controls', () => {
  it('exposes a partial image category as mixed and completes it without losing other selections', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn<(types: string[]) => void>()
    render(<FilterMimeTypes selectedTypes={['.png', '.mp4']} onChange={onChange} />)
    await user.click(screen.getByRole('combobox'))

    const images = await screen.findByRole('checkbox', { name: 'Images' })
    expect(images).toBePartiallyChecked()
    expect(images).toHaveAttribute('data-indeterminate')
    await user.click(images)

    expect(onChange).toHaveBeenCalledOnce()
    expect(onChange).toHaveBeenCalledWith([
      '.png', '.mp4', '.jpg', '.jpeg', '.gif', '.svg', '.webp', '.bmp', '.ico', '.tiff', '.psd',
    ])
  })

  it('exposes a fully selected category as checked and removes only that category', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn<(types: string[]) => void>()
    render(<FilterMimeTypes selectedTypes={[...imageExtensions, '.mp4']} onChange={onChange} />)
    await user.click(screen.getByRole('combobox'))

    const images = await screen.findByRole('checkbox', { name: 'Images' })
    expect(images).toBeChecked()
    expect(images).not.toHaveAttribute('data-indeterminate')
    await user.click(images)

    expect(onChange).toHaveBeenCalledOnce()
    expect(onChange).toHaveBeenCalledWith(['.mp4'])
  })

  it('exposes an empty category as unchecked and selects every image extension', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn<(types: string[]) => void>()
    render(<FilterMimeTypes selectedTypes={[]} onChange={onChange} />)
    await user.click(screen.getByRole('combobox'))

    const images = await screen.findByRole('checkbox', { name: 'Images' })
    expect(images).not.toBeChecked()
    expect(images).not.toHaveAttribute('data-indeterminate')
    await user.click(images)

    expect(onChange).toHaveBeenCalledOnce()
    expect(onChange).toHaveBeenCalledWith(imageExtensions)
  })

  it('emits the declared storage value from the actual Select', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn<(value: NonNullable<FilterConfig['storageType']>) => void>()
    render(<FilterSelect value="all" onChange={onChange} options={STORAGE_TYPE_OPTIONS} />)
    await user.click(screen.getByRole('combobox'))
    await user.click(await screen.findByRole('option', { name: 'Google Drive' }))

    expect(onChange).toHaveBeenCalledWith('google-drive')
  })

  it('emits the declared asset value from the actual Select', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn<(value: NonNullable<FilterConfig['assetType']>) => void>()
    render(<FilterSelect value="standard" onChange={onChange} options={ASSET_TYPE_OPTIONS} />)
    await user.click(screen.getByRole('combobox'))
    await user.click(await screen.findByRole('option', { name: 'Bundler' }))

    expect(onChange).toHaveBeenCalledWith('bundler')
  })

  it('emits the declared usage value from the actual Select', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn<(value: NonNullable<FilterConfig['usageFilter']>) => void>()
    render(<FilterSelect value="all" onChange={onChange} options={USAGE_FILTER_OPTIONS} />)
    await user.click(screen.getByRole('combobox'))
    await user.click(await screen.findByRole('option', { name: 'Unused' }))

    expect(onChange).toHaveBeenCalledWith('unused')
  })
})
