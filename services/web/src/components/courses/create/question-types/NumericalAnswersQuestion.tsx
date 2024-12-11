import { Control, useFieldArray } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"

interface NumericalAnswersQuestionProps {
  control: Control<any>;
  index: number;
}

export default function NumericalAnswersQuestion({ control, index }: NumericalAnswersQuestionProps) {
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
            {...control.register(`questions.${index}.answers.${answerIndex}.explain`)}
            placeholder="Explain"
            className="flex-grow"
          />
          <Input
            {...control.register(`questions.${index}.answers.${answerIndex}.value`, { valueAsNumber: true })}
            placeholder="Value"
            type="number"
            className="w-32"
          />
          <Button type="button" onClick={() => remove(answerIndex)} variant="destructive" size="sm">
            Remove
          </Button>
        </div>
      ))}
      <Button
        type="button"
        onClick={() => append({ order: fields.length + 1, explain: '', value: 0 })}
        variant="outline"
        size="sm"
      >
        Add Numerical Answer
      </Button>
    </div>
  );
}

