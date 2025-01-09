import { Control, useFieldArray } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Checkbox } from "@/components/ui/checkbox"

interface MatchingQuestionProps {
  control: Control<any>;
  index: number;
}

export default function MatchingQuestion({ control, index }: MatchingQuestionProps) {
  const { fields, append, remove } = useFieldArray({
    control,
    name: `questions.${index}.matching`
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center space-x-2">
        <Checkbox
          id={`questions.${index}.isRandomOrder`}
          {...control.register(`questions.${index}.isRandomOrder`)}
        />
        <Label htmlFor={`questions.${index}.isRandomOrder`}>Is Random Order?</Label>
      </div>
      
      <div className="grid grid-cols-3 gap-4 mb-2">
        <Label>Order</Label>
        <Label>Word A</Label>
        <Label>Word B</Label>
      </div>
      
      {fields.map((field, matchIndex) => (
        <div key={field.id} className="grid grid-cols-3 gap-4 items-center">
          <Input
            {...control.register(`questions.${index}.matching.${matchIndex}.order`)}
            placeholder="Order"
            type="number"
          />
          <Input
            {...control.register(`questions.${index}.matching.${matchIndex}.wordA`)}
            placeholder="Word A"
          />
          <div className="flex items-center space-x-2">
            <Input
              {...control.register(`questions.${index}.matching.${matchIndex}.wordB`)}
              placeholder="Word B"
            />
            <Button type="button" onClick={() => remove(matchIndex)} variant="destructive" size="sm">
              Remove
            </Button>
          </div>
        </div>
      ))}
      
      <Button
        type="button"
        onClick={() => append({ order: fields.length + 1, wordA: '', wordB: '' })}
        variant="outline"
        size="sm"
      >
        Add Matching Pair
      </Button>
    </div>
  );
}

