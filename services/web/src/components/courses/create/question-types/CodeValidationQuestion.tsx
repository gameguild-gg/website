import { Control, useFieldArray } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Textarea } from "@/components/ui/textarea"

interface CodeValidationQuestionProps {
  control: Control<any>;
  index: number;
}

export default function CodeValidationQuestion({ control, index }: CodeValidationQuestionProps) {
  const { fields, append, remove } = useFieldArray({
    control,
    name: `questions.${index}.codeValidation`
  });

  return (
    <div className="space-y-4">
      {fields.map((field, validationIndex) => (
        <div key={field.id} className="space-y-2">
          <div className="flex items-center space-x-2">
            <Input
              {...control.register(`questions.${index}.codeValidation.${validationIndex}.order`)}
              placeholder="Order"
              type="number"
              className="w-20"
            />
            <Select
              {...control.register(`questions.${index}.codeValidation.${validationIndex}.type`)}
            >
              <SelectTrigger className="w-[100px]">
                <SelectValue placeholder="Type" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="input">Input</SelectItem>
                <SelectItem value="output">Output</SelectItem>
              </SelectContent>
            </Select>
            <Button type="button" onClick={() => remove(validationIndex)} variant="destructive" size="sm">
              Remove
            </Button>
          </div>
          <Textarea
            {...control.register(`questions.${index}.codeValidation.${validationIndex}.content`)}
            placeholder="Enter input or output"
          />
        </div>
      ))}
      <Button
        type="button"
        onClick={() => append({ order: fields.length + 1, type: 'input', content: '' })}
        variant="outline"
        size="sm"
      >
        Add Validation
      </Button>
      <div>
        <Label>Correct Justify</Label>
        <Textarea
          {...control.register(`questions.${index}.correctJustify`)}
          placeholder="Correct Justify"
        />
      </div>
    </div>
  );
}

