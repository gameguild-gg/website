import { Control, useFieldArray } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Checkbox } from "@/components/ui/checkbox"

interface MultipleAnswersQuestionProps {
  control: Control<any>;
  index: number;
}

export default function MultipleAnswersQuestion({ control, index }: MultipleAnswersQuestionProps) {
  const { fields, append, remove } = useFieldArray({
    control,
    name: `questions.${index}.answers`
  });

  return (
    <div className="space-y-4">
      {fields.map((field, answerIndex) => (
        <div key={field.id} className="flex items-center space-x-2">
          <Input
            {...control.register(`questions.${index}.answers.${answerIndex}.order`)}
            placeholder="Order"
            type="number"
            className="w-20"
          />
          <Input
            {...control.register(`questions.${index}.answers.${answerIndex}.text`)}
            placeholder="Answer"
            className="flex-grow"
          />
          <Checkbox
            {...control.register(`questions.${index}.answers.${answerIndex}.isCorrect`)}
          />
          <Button type="button" onClick={() => remove(answerIndex)} variant="destructive" size="sm">
            Remove
          </Button>
        </div>
      ))}
      <Button
        type="button"
        onClick={() => append({ order: fields.length + 1, text: '', isCorrect: false })}
        variant="outline"
        size="sm"
      >
        Add Answer
      </Button>
    </div>
  );
}

