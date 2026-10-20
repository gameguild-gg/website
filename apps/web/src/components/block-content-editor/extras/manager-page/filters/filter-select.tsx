"use client"

import React from 'react'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@game-guild/ui/components/select"

export interface SelectOption<Value extends string = string> {
  readonly value: Value
  readonly label: string
}

interface FilterSelectProps<Value extends string> {
  value: Value
  onChange: (value: Value) => void
  options: readonly SelectOption<Value>[]
  placeholder?: string
  className?: string
}

export function FilterSelect<Value extends string>({
  value, 
  onChange, 
  options, 
  placeholder, 
  className = "w-[150px]" 
}: FilterSelectProps<Value>) {
  return (
    <Select<Value> value={value} onValueChange={(value) => value !== null && onChange(value)}>
      <SelectTrigger className={className}>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
