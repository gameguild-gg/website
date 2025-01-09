import { Control } from 'react-hook-form'
import { Textarea } from "@/components/ui/textarea"
import { Label } from "@/components/ui/label"

interface LongAnswerQuestionProps {
  control: Control<any>;
  index: number;
}

export default function LongAnswerQuestion({ control, index }: LongAnswerQuestionProps) {
  return (
    <div>
      <Label>Correct Answer</Label>
      <Textarea {...control.register(`questions.${index}.correctAnswer`)} placeholder="Correct Answer" />
    </div>
  );
}

