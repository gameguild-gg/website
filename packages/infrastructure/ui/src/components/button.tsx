"use client"

import { Slot } from "@radix-ui/react-slot"
import { Button as ButtonPrimitive } from "@base-ui/react/button"
import type { VariantProps } from "class-variance-authority"

import { buttonVariants } from "@game-guild/ui/components/button-variants"
import { cn } from "@game-guild/ui/lib/utils"

function Button({
  className,
  variant = "default",
  size = "default",
  asChild = false,
  children,
  render,
  nativeButton,
  ...props
}: ButtonPrimitive.Props & VariantProps<typeof buttonVariants> & {
  asChild?: boolean
}) {
  const buttonClassName = cn(buttonVariants({ variant, size, className }))

  if (asChild) {
    // Keep semantic anchors and Radix-compatible child/parent handler and ref
    // composition. Base UI's native render API remains available below.
    return <Slot data-slot="button" className={buttonClassName} {...props} style={typeof props.style === "function" ? props.style({ disabled: !!props.disabled }) : props.style}>{children}</Slot>
  }

  return (
    <ButtonPrimitive
      data-slot="button"
      className={buttonClassName}
      render={render}
      nativeButton={nativeButton}
      {...props}
    >
      {children}
    </ButtonPrimitive>
  )
}

export { Button, buttonVariants }
