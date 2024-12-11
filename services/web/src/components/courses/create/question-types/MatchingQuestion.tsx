import { Control, useFieldArray } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Textarea } from "@/components/ui/textarea"

interface MatchingQuestionProps {
  control: Control<any>;
  index: number;
};

export default function MatchingQuestion({ control, index }: MatchingQuestionProps) {
  const { fields, append, remove } = useFieldArray({
    control,
    name: `questions.${index}.matching`
  });

  return (
    <div className="space-y-4">
      {fields.map((field, matchIndex) => (
        <div key={field.id} className="space-y-2">
          <div className="flex items-center space-x-2">
            <Input
              {...control.register(`questions.${index}.matching.${matchIndex}.col`)}
              placeholder="Column"
              type="number"
              className="w-20"
            />
            <Input
              {...control.register(`questions.${index}.matching.${matchIndex}.order`)}
              placeholder="Order"
              type="number"
              className="w-20"
            />
            <Input
              {...control.register(`questions.${index}.matching.${matchIndex}.option`)}
              placeholder="Option"
              className="flex-grow"
            />
            <Select
              {...control.register(`questions.${index}.matching.${matchIndex}.matchesWith`)}
            >
              <SelectTrigger className="w-[180px]">
                <SelectValue placeholder="Matches With" />
              </SelectTrigger>
              <SelectContent>
                {fields
                  .filter(m => m.col !== field.col)
                  .map(m => (
                    <SelectItem key={m.id} value={m.order.toString()}>
                      {m.option}
                    </SelectItem>
                  ))
                }
              </SelectContent>
            </Select>
            <Button type="button" onClick={() => remove(matchIndex)} variant="destructive" size="sm">
              Remove
            </Button>
          </div>
        </div>
      ))}
      <Button
        type="button"
        onClick={() => append({ col: 1, order: fields.length + 1, option: '', matchesWith: '' })}
        variant="outline"
        size="sm"
      >
        Add Matching
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

