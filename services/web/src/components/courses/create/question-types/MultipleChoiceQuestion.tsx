import { Control, useFieldArray } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Checkbox } from "@/components/ui/checkbox"
import { Textarea } from "@/components/ui/textarea"

interface MultipleChoiceQuestionProps {
  control: Control<any>;
  index: number;
}

export default function MultipleChoiceQuestion({ control, index }: MultipleChoiceQuestionProps) {
  const { fields, append, remove } = useFieldArray({
    control,
    name: `questions.${index}.options`
  });

  return (
    <div className="space-y-4">
      {fields.map((field, optionIndex) => (
        <div key={field.id} className="space-y-2">
          <div className="flex items-center space-x-2">
            <Input
              {...control.register(`questions.${index}.options.${optionIndex}.order`)}
              placeholder="Order"
              type="number"
              className="w-20"
            />
            <Input
              {...control.register(`questions.${index}.options.${optionIndex}.text`)}
              placeholder="Option"
              className="flex-grow"
            />
            <Checkbox
              {...control.register(`questions.${index}.options.${optionIndex}.isCorrect`)}
            />
            <Button type="button" onClick={() => remove(optionIndex)} variant="destructive" size="sm">
              Remove
            </Button>
          </div>
        </div>
      ))}
      <Button
        type="button"
        onClick={() => append({ order: fields.length + 1, text: '', isCorrect: false })}
        variant="outline"
        size="sm"
      >
        Add Option
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

