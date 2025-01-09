import { Control, useFieldArray } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"

interface ShortAnswerQuestionProps {
  control: Control<any>;
  index: number;
}

export default function ShortAnswerQuestion({ control, index }: ShortAnswerQuestionProps) {
  const { fields, append, remove } = useFieldArray({
    control,
    name: `questions.${index}.answers`
  });

  return (
    <div className="space-y-4">
      <div>
        <Label>Correct Answer</Label>
        <Input {...control.register(`questions.${index}.correctAnswer`)} placeholder="Correct Answer" />
      </div>
      <div>
        <Label>Character Limit</Label>
        <Input 
          type="number" 
          {...control.register(`questions.${index}.charsLimit`, { valueAsNumber: true })} 
          placeholder="Character Limit" 
        />
      </div>
    </div>
  );
}

