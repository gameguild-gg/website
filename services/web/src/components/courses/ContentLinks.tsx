import { useFieldArray, Control } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"

interface ContentLinksProps {
  control: Control<any>;
  name: string;
}

export default function ContentLinks({ control, name }: ContentLinksProps) {
  const { fields, append, remove } = useFieldArray({
    control,
    name: name,
  });

  return (
    <div>
      <Label>Content Links</Label>
      {fields.map((field, index) => (
        <div key={field.id} className="flex items-center space-x-2 mt-2">
          <Input
            {...control.register(`${name}.${index}.order`)}
            placeholder="Order"
            type="number"
            className="w-20"
          />
          <Input
            {...control.register(`${name}.${index}.link`)}
            placeholder="Link"
            className="flex-grow"
          />
          <Button type="button" onClick={() => remove(index)} variant="destructive" size="sm">
            Remove
          </Button>
        </div>
      ))}
      <Button
        type="button"
        onClick={() => append({ order: '', link: '' })}
        variant="outline"
        size="sm"
        className="mt-2"
      >
        Add Content Link
      </Button>
    </div>
  );
}

