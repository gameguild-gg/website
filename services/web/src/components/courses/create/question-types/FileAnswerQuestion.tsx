import { Control, useFieldArray } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"

interface FileAnswerQuestionProps {
  control: Control<any>;
  index: number;
}

export default function FileAnswerQuestion({ control, index }: FileAnswerQuestionProps) {
  const { fields, append, remove } = useFieldArray({
    control,
    name: `questions.${index}.fileAnswers`
  });

  return (
    <div className="space-y-4">
      {fields.map((field, fileIndex) => (
        <div key={field.id} className="space-y-2">
          <div className="flex items-center space-x-2">
            <Input
              {...control.register(`questions.${index}.fileAnswers.${fileIndex}.order`)}
              placeholder="Order"
              type="number"
              className="w-20"
            />
            <Input
              {...control.register(`questions.${index}.fileAnswers.${fileIndex}.contentLink`)}
              placeholder="Content Link"
              className="flex-grow"
            />
            <Button type="button" onClick={() => remove(fileIndex)} variant="destructive" size="sm">
              Remove
            </Button>
          </div>
        </div>
      ))}
      <Button
        type="button"
        onClick={() => append({ order: fields.length + 1, contentLink: '' })}
        variant="outline"
        size="sm"
      >
        Add File Answer
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

