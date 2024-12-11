import { Control, useFieldArray } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"

interface CompleteSentenceQuestionProps {
  control: Control<any>;
  index: number;
}

export default function CompleteSentenceQuestion({ control, index }: CompleteSentenceQuestionProps) {
  const { fields, append, remove } = useFieldArray({
    control,
    name: `questions.${index}.words`
  });

  return (
    <div className="space-y-4">
      <div>
        <Label>Sentence</Label>
        <Textarea 
          {...control.register(`questions.${index}.sentence`)} 
          placeholder="Enter sentence with placeholders (e.g., The <1> jumped over the <2>.)" 
        />
      </div>
      {fields.map((field, wordIndex) => (
        <div key={field.id} className="flex items-center space-x-2">
          <Input
            {...control.register(`questions.${index}.words.${wordIndex}.order`)}
            placeholder="Order"
            type="number"
            className="w-20"
          />
          <Input
            {...control.register(`questions.${index}.words.${wordIndex}.value`)}
            placeholder="Value"
            className="w-32"
          />
          <Input
            {...control.register(`questions.${index}.words.${wordIndex}.word`)}
            placeholder="Word"
            className="flex-grow"
          />
          <Select
            {...control.register(`questions.${index}.words.${wordIndex}.isVisible`)}
          >
            <SelectTrigger className="w-[180px]">
              <SelectValue placeholder="Visibility" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="visible">Visible</SelectItem>
              <SelectItem value="hidden">Need to Write</SelectItem>
            </SelectContent>
          </Select>
          <Button type="button" onClick={() => remove(wordIndex)} variant="destructive" size="sm">
            Remove
          </Button>
        </div>
      ))}
      <Button
        type="button"
        onClick={() => append({ order: fields.length + 1, value: '', word: '', isVisible: 'visible' })}
        variant="outline"
        size="sm"
      >
        Add Word
      </Button>
    </div>
  );
}

