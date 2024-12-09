'use client'

import { useEffect } from 'react'
import { useForm, useFieldArray } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"

interface PriceInstallment {
  installments: number;
  price: number;
}

interface PricingData {
  priceInstallments: PriceInstallment[];
  fullPrice: number;
  maxDiscount: number;
  maxUnits: number;
  launchDate: string;
  closingDate: string;
}

export default function PricingTab({ data, updateData }: { data: PricingData; updateData: (data: PricingData) => void }) {
  const { register, control, handleSubmit, reset, watch } = useForm<PricingData>({
    defaultValues: {
      priceInstallments: [{ installments: 1, price: 0 }],
      fullPrice: 0,
      maxDiscount: 0,
      maxUnits: 0,
      launchDate: '',
      closingDate: '',
    }
  })

  const { fields, append, remove } = useFieldArray({
    control,
    name: "priceInstallments"
  })

  useEffect(() => {
    reset(data)
  }, [data, reset])

  const onSubmit = (formData: PricingData) => {
    updateData(formData)
  }

  const watchPriceInstallments = watch("priceInstallments")

  return (
    <div className="grid md:grid-cols-2 gap-4">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <Label>Price Installments</Label>
          {fields.map((field, index) => (
            <div key={field.id} className="flex items-center space-x-2 mt-2">
              <Input
                {...register(`priceInstallments.${index}.installments` as const, {
                  min: 1,
                  max: 12,
                  valueAsNumber: true
                })}
                placeholder="Installments"
                type="number"
                min={1}
                max={12}
              />
              <Input
                {...register(`priceInstallments.${index}.price` as const, {
                  min: 0,
                  valueAsNumber: true
                })}
                placeholder="Price"
                type="number"
                step="0.01"
                min={0}
              />
              <Button type="button" onClick={() => remove(index)} variant="destructive" size="sm">
                Remove
              </Button>
            </div>
          ))}
          <Button
            type="button"
            onClick={() => append({ installments: 1, price: 0 })}
            variant="outline"
            size="sm"
            className="mt-2"
            disabled={fields.length >= 12}
          >
            Add Price Installment
          </Button>
        </div>
        <div>
          <Label htmlFor="fullPrice">Full Price</Label>
          <Input id="fullPrice" type="number" step="0.01" {...register('fullPrice', { min: 0, valueAsNumber: true })} />
        </div>
        <div>
          <Label htmlFor="maxDiscount">Maximum Discount</Label>
          <Input id="maxDiscount" type="number" step="0.01" {...register('maxDiscount', { min: 0, valueAsNumber: true })} />
        </div>
        <div>
          <Label htmlFor="maxUnits">Maximum Units for Sale</Label>
          <Input id="maxUnits" type="number" {...register('maxUnits', { min: 0, valueAsNumber: true })} />
        </div>
        <div>
          <Label htmlFor="launchDate">Launch Date and Time</Label>
          <Input id="launchDate" type="datetime-local" {...register('launchDate')} />
        </div>
        <div>
          <Label htmlFor="closingDate">Closing Date and Time</Label>
          <Input id="closingDate" type="datetime-local" {...register('closingDate')} />
        </div>
        <Button type="submit">Save Data</Button>
      </form>
      <div>
        <h3 className="text-lg font-semibold mb-2">Pricing Preview</h3>
        <div className="space-y-2">
          <p><strong>Full Price:</strong> ${data.fullPrice}</p>
          <div>
            <strong>Price Installments:</strong>
            <ul className="list-disc pl-5">
              {watchPriceInstallments.map((item, index) => (
                <li key={index}>
                  {item.installments}x: ${item.price}
                </li>
              ))}
            </ul>
          </div>
          <p><strong>Max Discount:</strong> ${data.maxDiscount}</p>
          <p><strong>Max Units:</strong> {data.maxUnits}</p>
          <p><strong>Launch Date:</strong> {data.launchDate}</p>
          <p><strong>Closing Date:</strong> {data.closingDate}</p>
        </div>
      </div>
    </div>
  )
}

