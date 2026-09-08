"use client"

import { Accordion as AccordionPrimitive } from "@base-ui/react/accordion"

import { cn } from "@game-guild/ui/lib/utils"
import { ChevronDownIcon, ChevronUpIcon } from "lucide-react"

type AccordionProps =
  | (AccordionPrimitive.Root.Props<string> & { type?: undefined; collapsible?: boolean })
  | (Omit<AccordionPrimitive.Root.Props<string>, "value" | "defaultValue" | "onValueChange" | "multiple"> & {
      type: "single"; collapsible?: boolean; value?: string; defaultValue?: string; onValueChange?: (value: string) => void
    })
  | (AccordionPrimitive.Root.Props<string> & { type: "multiple"; collapsible?: boolean })

function Accordion({ className, ...props }: AccordionProps) {
  const { type, collapsible, value, defaultValue, onValueChange, ...rest } = props
  const single = type === "single"
  return (
    <AccordionPrimitive.Root
      data-slot="accordion"
      className={cn("flex w-full flex-col", className)}
      {...rest}
      multiple={type === "multiple" || (!single && ('multiple' in rest ? rest.multiple : false))}
      value={single ? (value === undefined ? undefined : value ? [value as string] : []) : value as string[] | undefined}
      defaultValue={single ? (defaultValue ? [defaultValue as string] : []) : defaultValue as string[] | undefined}
      onValueChange={(values, details) => {
        if (single) {
          if (!collapsible && values.length === 0) { details.cancel(); return }
          ;(onValueChange as ((value: string) => void) | undefined)?.(values[0] ?? "")
        } else {
          ;(onValueChange as AccordionPrimitive.Root.Props<string>["onValueChange"])?.(values, details)
        }
      }}
    />
  )
}

function AccordionItem({ className, ...props }: AccordionPrimitive.Item.Props) {
  return (
    <AccordionPrimitive.Item
      data-slot="accordion-item"
      className={cn("not-last:border-b", className)}
      {...props}
    />
  )
}

function AccordionTrigger({
  className,
  children,
  ...props
}: AccordionPrimitive.Trigger.Props) {
  return (
    <AccordionPrimitive.Header className="flex">
      <AccordionPrimitive.Trigger
        data-slot="accordion-trigger"
        className={cn(
          "group/accordion-trigger relative flex flex-1 items-start justify-between rounded-md border border-transparent py-4 text-left text-sm font-medium transition-all outline-none hover:underline focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:after:border-ring aria-disabled:pointer-events-none aria-disabled:opacity-50 **:data-[slot=accordion-trigger-icon]:ml-auto **:data-[slot=accordion-trigger-icon]:size-4 **:data-[slot=accordion-trigger-icon]:text-muted-foreground",
          className
        )}
        {...props}
      >
        {children}
        <ChevronDownIcon data-slot="accordion-trigger-icon" className="pointer-events-none shrink-0 group-aria-expanded/accordion-trigger:hidden" />
        <ChevronUpIcon data-slot="accordion-trigger-icon" className="pointer-events-none hidden shrink-0 group-aria-expanded/accordion-trigger:inline" />
      </AccordionPrimitive.Trigger>
    </AccordionPrimitive.Header>
  )
}

function AccordionContent({
  className,
  children,
  ...props
}: AccordionPrimitive.Panel.Props) {
  return (
    <AccordionPrimitive.Panel
      data-slot="accordion-content"
      className="overflow-hidden text-sm data-open:animate-accordion-down data-closed:animate-accordion-up"
      {...props}
    >
      <div
        className={cn(
          "h-(--accordion-panel-height) pt-0 pb-4 data-ending-style:h-0 data-starting-style:h-0 [&_a]:underline [&_a]:underline-offset-3 [&_a]:hover:text-foreground [&_p:not(:last-child)]:mb-4",
          className
        )}
      >
        {children}
      </div>
    </AccordionPrimitive.Panel>
  )
}

export { Accordion, AccordionItem, AccordionTrigger, AccordionContent }
