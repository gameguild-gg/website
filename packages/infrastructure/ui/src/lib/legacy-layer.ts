"use client"

import * as React from "react"

export type LegacyLayerEventHandler = (event: Event) => void
export interface LegacyLayerHandlers {
  onOpenAutoFocus?: LegacyLayerEventHandler
  onCloseAutoFocus?: LegacyLayerEventHandler
  onPointerDownOutside?: LegacyLayerEventHandler
  onFocusOutside?: LegacyLayerEventHandler
  onInteractOutside?: LegacyLayerEventHandler
  onEscapeKeyDown?: LegacyLayerEventHandler
}

interface ChangeDetails {
  reason: string
  event: Event | React.SyntheticEvent
  cancel(): void
}

export const LegacyLayerContext = React.createContext<React.RefObject<LegacyLayerHandlers> | null>(null)

// Cancellation must reach the primitive's own state transition. Document-level
// listeners cannot reliably prevent Base UI dismissal, and also observe closed
// or unrelated popups. Each root owns exactly its own content handlers.
export function useLegacyLayerRoot<Details extends ChangeDetails>(onOpenChange?: (open: boolean, details: Details) => void) {
  const handlers = React.useRef<LegacyLayerHandlers>({})
  const handleOpenChange = (open: boolean, details: Details) => {
    if (!open) {
      const original = "nativeEvent" in details.event ? details.event.nativeEvent : details.event
      const event = details.reason === "escape-key" ? original : new CustomEvent(details.reason, {
        cancelable: true, detail: { originalEvent: original },
      })
      if (details.reason === "escape-key") handlers.current.onEscapeKeyDown?.(event)
      if (details.reason === "outside-press") {
        handlers.current.onPointerDownOutside?.(event)
        handlers.current.onInteractOutside?.(event)
      }
      if (details.reason === "focus-out") {
        handlers.current.onFocusOutside?.(event)
        handlers.current.onInteractOutside?.(event)
      }
      if (event.defaultPrevented) { details.cancel(); return }
    }
    onOpenChange?.(open, details)
  }
  return { handlers, onOpenChange: handleOpenChange }
}

export function useMergedRefs<T>(...refs: Array<React.Ref<T> | undefined>) {
  return React.useCallback((value: T | null) => {
    const cleanups = refs.map(ref => {
      if (typeof ref === "function") {
        const cleanup = ref(value)
        return typeof cleanup === "function" ? cleanup : () => ref(null)
      }
      if (ref) {
        ref.current = value
        return () => { ref.current = null }
      }
      return undefined
    })
    return () => cleanups.forEach(cleanup => cleanup?.())
  }, refs)
}

export function useLegacyLayerHandlers(
  _elementRef: React.RefObject<HTMLElement | null>,
  handlers: LegacyLayerHandlers
) {
  const context = React.useContext(LegacyLayerContext)
  React.useLayoutEffect(() => {
    if (!context) return
    context.current = handlers
    return () => { if (context.current === handlers) context.current = {} }
  }, [context, handlers])
  const focus = (callback: LegacyLayerEventHandler | undefined, name: string) => {
    const event = new Event(name, { cancelable: true })
    callback?.(event)
    return !event.defaultPrevented
  }
  return {
    initialFocus: () => focus(handlers.onOpenAutoFocus, "openAutoFocus"),
    finalFocus: () => focus(handlers.onCloseAutoFocus, "closeAutoFocus"),
  }
}
