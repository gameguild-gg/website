"use client"

import * as React from "react"
import { Popover as PopoverPrimitive } from "@base-ui/react/popover"

import { cn } from "@game-guild/ui/lib/utils"
import { type LegacyLayerHandlers, LegacyLayerContext, useLegacyLayerRoot, useLegacyLayerHandlers, useMergedRefs } from "@game-guild/ui/lib/legacy-layer"

function Popover({ onOpenChange, ...props }: PopoverPrimitive.Root.Props) {
  const layer = useLegacyLayerRoot(onOpenChange)
  return <LegacyLayerContext.Provider value={layer.handlers}><PopoverPrimitive.Root data-slot="popover" {...props} onOpenChange={layer.onOpenChange} /></LegacyLayerContext.Provider>
}

function PopoverTrigger({ asChild, children, render, ...props }: PopoverPrimitive.Trigger.Props & { asChild?: boolean }) {
  return <PopoverPrimitive.Trigger data-slot="popover-trigger" render={asChild && React.isValidElement(children) ? children : render} {...props}>{asChild ? null : children}</PopoverPrimitive.Trigger>
}

function PopoverContent({
  className,
  align = "center",
  alignOffset = 0,
  side = "bottom",
  sideOffset = 4,
  ref,
  onOpenAutoFocus,
  onCloseAutoFocus,
  onPointerDownOutside,
  onFocusOutside,
  onInteractOutside,
  onEscapeKeyDown,
  ...props
}: PopoverPrimitive.Popup.Props &
  Pick<
    PopoverPrimitive.Positioner.Props,
    "align" | "alignOffset" | "side" | "sideOffset"
  > & LegacyLayerHandlers) {
  const popupRef = React.useRef<HTMLDivElement>(null)
  const focusProps = useLegacyLayerHandlers(popupRef, {
    onOpenAutoFocus,
    onCloseAutoFocus,
    onPointerDownOutside,
    onFocusOutside,
    onInteractOutside,
    onEscapeKeyDown,
  })
  const mergedRef = useMergedRefs(ref, popupRef)
  return (
    <PopoverPrimitive.Portal>
      <PopoverPrimitive.Positioner
        align={align}
        alignOffset={alignOffset}
        side={side}
        sideOffset={sideOffset}
        className="isolate z-50"
      >
        <PopoverPrimitive.Popup
          ref={mergedRef}
          {...focusProps}
          data-slot="popover-content"
          className={cn(
            "z-50 flex w-72 origin-(--transform-origin) flex-col gap-4 rounded-md bg-popover p-4 text-sm text-popover-foreground shadow-md ring-1 ring-foreground/10 outline-hidden duration-100 data-[side=bottom]:slide-in-from-top-2 data-[side=inline-end]:slide-in-from-left-2 data-[side=inline-start]:slide-in-from-right-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 data-open:animate-in data-open:fade-in-0 data-open:zoom-in-95 data-closed:animate-out data-closed:fade-out-0 data-closed:zoom-out-95",
            className
          )}
          {...props}
        />
      </PopoverPrimitive.Positioner>
    </PopoverPrimitive.Portal>
  )
}

function PopoverHeader({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="popover-header"
      className={cn("flex flex-col gap-1 text-sm", className)}
      {...props}
    />
  )
}

function PopoverTitle({ className, ...props }: PopoverPrimitive.Title.Props) {
  return (
    <PopoverPrimitive.Title
      data-slot="popover-title"
      className={cn("font-medium", className)}
      {...props}
    />
  )
}

function PopoverDescription({
  className,
  ...props
}: PopoverPrimitive.Description.Props) {
  return (
    <PopoverPrimitive.Description
      data-slot="popover-description"
      className={cn("text-muted-foreground", className)}
      {...props}
    />
  )
}

export {
  Popover,
  PopoverContent,
  PopoverDescription,
  PopoverHeader,
  PopoverTitle,
  PopoverTrigger,
}
