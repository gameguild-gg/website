"use client"

import * as React from "react"
import { Dialog as DialogPrimitive } from "@base-ui/react/dialog"

import { cn } from "@game-guild/ui/lib/utils"
import { type LegacyLayerHandlers, LegacyLayerContext, useLegacyLayerRoot, useLegacyLayerHandlers, useMergedRefs } from "@game-guild/ui/lib/legacy-layer"
import { Button } from "@game-guild/ui/components/button"
import { XIcon } from "lucide-react"

function Dialog({ onOpenChange, ...props }: DialogPrimitive.Root.Props) {
  const layer = useLegacyLayerRoot(onOpenChange)
  return <LegacyLayerContext.Provider value={layer.handlers}><DialogPrimitive.Root data-slot="dialog" {...props} onOpenChange={layer.onOpenChange} /></LegacyLayerContext.Provider>
}

function DialogTrigger({ asChild, children, render, ...props }: DialogPrimitive.Trigger.Props & { asChild?: boolean }) {
  return <DialogPrimitive.Trigger data-slot="dialog-trigger" render={asChild && React.isValidElement(children) ? children : render} {...props}>{asChild ? null : children}</DialogPrimitive.Trigger>
}

function DialogPortal({ ...props }: DialogPrimitive.Portal.Props) {
  return <DialogPrimitive.Portal data-slot="dialog-portal" {...props} />
}

function DialogClose({ asChild, children, render, ...props }: DialogPrimitive.Close.Props & { asChild?: boolean }) {
  return <DialogPrimitive.Close data-slot="dialog-close" render={asChild && React.isValidElement(children) ? children : render} {...props}>{asChild ? null : children}</DialogPrimitive.Close>
}

function DialogOverlay({
  className,
  ...props
}: DialogPrimitive.Backdrop.Props) {
  return (
    <DialogPrimitive.Backdrop
      data-slot="dialog-overlay"
      className={cn(
        "fixed inset-0 isolate z-50 bg-black/10 duration-100 supports-backdrop-filter:backdrop-blur-xs data-open:animate-in data-open:fade-in-0 data-closed:animate-out data-closed:fade-out-0",
        className
      )}
      {...props}
    />
  )
}

function DialogContent({
  className,
  children,
  showCloseButton = true,
  ref,
  onOpenAutoFocus,
  onCloseAutoFocus,
  onPointerDownOutside,
  onFocusOutside,
  onInteractOutside,
  onEscapeKeyDown,
  ...props
}: DialogPrimitive.Popup.Props & {
  showCloseButton?: boolean
} & LegacyLayerHandlers) {
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
    <DialogPortal>
      <DialogOverlay />
      <DialogPrimitive.Popup
        ref={mergedRef}
          {...focusProps}
        data-slot="dialog-content"
        className={cn(
          "fixed top-1/2 left-1/2 z-50 grid w-full max-w-[calc(100%-2rem)] -translate-x-1/2 -translate-y-1/2 gap-6 rounded-xl bg-popover p-6 text-sm text-popover-foreground ring-1 ring-foreground/10 duration-100 outline-none sm:max-w-md data-open:animate-in data-open:fade-in-0 data-open:zoom-in-95 data-closed:animate-out data-closed:fade-out-0 data-closed:zoom-out-95",
          className
        )}
        {...props}
      >
        {children}
        {showCloseButton && (
          <DialogPrimitive.Close
            data-slot="dialog-close"
            render={
              <Button
                variant="ghost"
                className="absolute top-4 right-4"
                size="icon-sm"
              />
            }
          >
            <XIcon
            />
            <span className="sr-only">Close</span>
          </DialogPrimitive.Close>
        )}
      </DialogPrimitive.Popup>
    </DialogPortal>
  )
}

function DialogHeader({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="dialog-header"
      className={cn("flex flex-col gap-2", className)}
      {...props}
    />
  )
}

function DialogFooter({
  className,
  showCloseButton = false,
  children,
  ...props
}: React.ComponentProps<"div"> & {
  showCloseButton?: boolean
}) {
  return (
    <div
      data-slot="dialog-footer"
      className={cn(
        "flex flex-col-reverse gap-2 sm:flex-row sm:justify-end",
        className
      )}
      {...props}
    >
      {children}
      {showCloseButton && (
        <DialogPrimitive.Close render={<Button variant="outline" />}>
          Close
        </DialogPrimitive.Close>
      )}
    </div>
  )
}

function DialogTitle({ className, ...props }: DialogPrimitive.Title.Props) {
  return (
    <DialogPrimitive.Title
      data-slot="dialog-title"
      className={cn("leading-none font-medium", className)}
      {...props}
    />
  )
}

function DialogDescription({
  className,
  ...props
}: DialogPrimitive.Description.Props) {
  return (
    <DialogPrimitive.Description
      data-slot="dialog-description"
      className={cn(
        "text-sm text-muted-foreground *:[a]:underline *:[a]:underline-offset-3 *:[a]:hover:text-foreground",
        className
      )}
      {...props}
    />
  )
}

export {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogOverlay,
  DialogPortal,
  DialogTitle,
  DialogTrigger,
}
