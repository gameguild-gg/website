"use client"

import * as React from "react"
import { ChevronLeft, ChevronRight } from "lucide-react"
import { DayPicker, MonthCaptionProps, CalendarMonth } from "react-day-picker"

import { cn } from "@/lib/utils"
import { buttonVariants } from "@/components/ui/button"

export type CalendarProps = React.ComponentProps<typeof DayPicker>

// Função para obter o mês e ano a partir de `CalendarMonth`
function getDateFromCalendarMonth(calendarMonth: CalendarMonth) {
  return calendarMonth.date // Usando a propriedade `date` que é um objeto `Date`
}

// Componente de legenda customizado
function CustomCaption({ calendarMonth }: MonthCaptionProps) {
  const date = getDateFromCalendarMonth(calendarMonth)

  return (
    <div className="flex justify-between items-center px-2">
      <span className="text-sm font-medium">
        {date.toLocaleString("default", { month: "long", year: "numeric" })}
      </span>
    </div>
  )
}

function Calendar({
  className,
  classNames,
  showOutsideDays = true,
  ...props
}: CalendarProps) {
  const [month, setMonth] = React.useState(new Date())

  return (
    <DayPicker
      showOutsideDays={showOutsideDays}
      month={month}
      onMonthChange={setMonth} // Atualiza o mês ao navegar
      className={cn("p-3", className)}
      classNames={{
        months: "flex flex-col sm:flex-row space-y-4 sm:space-x-4 sm:space-y-0",
        month: "space-y-4",
        caption: "flex justify-center pt-1 relative items-center",
        caption_label: "text-sm font-medium",
        nav: "space-x-1 flex items-center",
        nav_button: cn(
          buttonVariants({ variant: "outline" }),
          "h-7 w-7 bg-transparent p-0 opacity-50 hover:opacity-100"
        ),
        table: "w-full border-collapse space-y-1",
        head_row: "flex",
        head_cell: "text-muted-foreground rounded-md w-9 font-normal text-[0.8rem]",
        row: "flex w-full mt-2",
        cell: "h-9 w-9 text-center text-sm p-0 relative [&:has([aria-selected].day-range-end)]:rounded-r-md [&:has([aria-selected].day-outside)]:bg-accent/50 [&:has([aria-selected])]:bg-accent first:[&:has([aria-selected])]:rounded-l-md last:[&:has([aria-selected])]:rounded-r-md focus-within:relative focus-within:z-20",
        day: cn(
          buttonVariants({ variant: "ghost" }),
          "h-9 w-9 p-0 font-normal aria-selected:opacity-100"
        ),
        ...classNames,
      }}
      components={{
        // Substituindo `caption` para usar `MonthCaption`
        MonthCaption: CustomCaption, // Usa o componente customizado de legenda
      }}
      {...props}
    />
  )
}

Calendar.displayName = "Calendar"

export { Calendar }
