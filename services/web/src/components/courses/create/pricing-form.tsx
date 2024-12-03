import { useState, useEffect } from 'react'
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { useCourseStore } from '@/store/courseStore'

export function PricingForm() {
  const { pricing, setPricing } = useCourseStore()
  const [formData, setFormData] = useState(pricing)

  useEffect(() => {
    setFormData(pricing)
  }, [pricing])

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target
    setFormData(prev => ({ ...prev, [name]: value }))
  }

  const handleInstallmentChange = (index: number, value: string) => {
    const newInstallments = [...formData.installments]
    newInstallments[index] = { number: index + 2, price: parseFloat(value) }
    setFormData(prev => ({ ...prev, installments: newInstallments }))
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setPricing(formData)
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Pricing Information</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <Input
            name="fullPrice"
            type="number"
            placeholder="Full Price"
            value={formData.fullPrice}
            onChange={handleInputChange}
            required
          />
          <div className="grid grid-cols-2 gap-4">
            {Array.from({ length: 11 }).map((_, index) => (
              <Input
                key={index}
                type="number"
                placeholder={`${index + 2}x installment`}
                value={formData.installments[index]?.price || ''}
                onChange={(e) => handleInstallmentChange(index, e.target.value)}
              />
            ))}
          </div>
          <Input
            name="maxDiscount"
            type="number"
            placeholder="Maximum Discount (%)"
            value={formData.maxDiscount}
            onChange={handleInputChange}
            required
          />
          <Input
            name="maxUnits"
            type="number"
            placeholder="Maximum Units for Sale"
            value={formData.maxUnits}
            onChange={handleInputChange}
            required
          />
          <Input
            name="publishDate"
            type="datetime-local"
            placeholder="Publish Date and Time"
            value={formData.publishDate}
            onChange={handleInputChange}
            required
          />
          <Input
            name="unpublishDate"
            type="datetime-local"
            placeholder="Unpublish Date and Time"
            value={formData.unpublishDate}
            onChange={handleInputChange}
            required
          />
        </CardContent>
      </Card>
      <Button type="submit">Update Pricing</Button>
    </form>
  )
}

