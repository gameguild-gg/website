'use client'

import { useEffect, useState } from 'react'
import { useForm, useFieldArray, Controller } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible"
import { ChevronDown, ChevronRight } from 'lucide-react'
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card"

interface PriceInstallment {
  installments: number;
  price: number;
}

interface MaximumDiscount {
  discount: string;
  period: string;
  maxSaleUnits: string;
}

interface PricingData {
  fullPrice: number | null;
  priceInstallments: { [key: number]: number | null };
  maximumDiscounts: MaximumDiscount[];
  launchDate: string;
  closingDate: string;
  platformFee: number;
  interestOption: 'interest free' | 'with interest';
  interestPercentage: number | null;
}

const discountOptions = ['25%', '33%', '50%', '66%', '75%', '90%', '100%'];
const periodOptions = ['no discount', '1x for year', '2x for year', '4x for year', '6x for year', '12x for year', 'unlimited'];
const maxSaleUnitsOptions = ['10', '25', '50', '100', '250', '500', '1000', 'unlimited', 'custom'];

const calculateCompoundInterest = (principal: number, rate: number, time: number) => {
  return principal * Math.pow(1 + rate, time);
};

export default function PricingTab({ data, updateData, setUnsavedChanges }: { data: PricingData; updateData: (data: PricingData) => void; setUnsavedChanges: (unsaved: boolean) => void }) {
  const [isMaxDiscountOpen, setIsMaxDiscountOpen] = useState(false);
  const [isPlatformRelationshipOpen, setIsPlatformRelationshipOpen] = useState(false);
  const [isPricingConfirmedOpen, setIsPricingConfirmedOpen] = useState(true);
  const [isPricingPreviewOpen, setIsPricingPreviewOpen] = useState(false);

  const { register, control, handleSubmit, reset, watch, setValue } = useForm<PricingData>({
    defaultValues: {
      fullPrice: null,
      priceInstallments: {1: null, 2: null, 3: null, 4: null, 6: null, 12: null},
      maximumDiscounts: discountOptions.map(discount => ({
        discount,
        period: 'no discount',
        maxSaleUnits: '10'
      })),
      launchDate: '',
      closingDate: '',
      platformFee: 12,
      interestOption: 'interest free',
      interestPercentage: null,
    }
  })

  const { fields: maximumDiscountFields, update: updateMaximumDiscount } = useFieldArray({
    control,
    name: "maximumDiscounts"
  })

  useEffect(() => {
    if (data) {
      reset({
        ...data,
        fullPrice: data.fullPrice || null,
        priceInstallments: data.priceInstallments || {1: null, 2: null, 3: null, 4: null, 6: null, 12: null},
        maximumDiscounts: data.maximumDiscounts && data.maximumDiscounts.length > 0
          ? data.maximumDiscounts
          : discountOptions.map(discount => ({
              discount,
              period: 'no discount',
              maxSaleUnits: '10'
            })),
        platformFee: data.platformFee || 12, 
        interestOption: data.interestOption || 'interest free',
        interestPercentage: data.interestPercentage || null,
      })
    }
  }, [data, reset])

  const onSubmit = (formData: PricingData) => {
    updateData(formData)
    setUnsavedChanges(false)
    setIsPricingConfirmedOpen(true)
    setIsPricingPreviewOpen(false)
  }

  const watchPriceInstallments = watch("priceInstallments")
  const watchFullPrice = watch("fullPrice")
  const watchMaximumDiscounts = watch("maximumDiscounts")

  useEffect(() => {
    // Auto-save whenever form values change
    const subscription = watch((value, { name, type }) => {
      if (name) {
        setUnsavedChanges(true)
        setIsPricingConfirmedOpen(false)
        setIsPricingPreviewOpen(true)
      }
    })
    return () => subscription.unsubscribe()
  }, [watch, setUnsavedChanges])

  const calculateFinalPrice = (price: number) => {
    const platformFee = price * (watch('platformFee') / 100); 
    return price - platformFee;
  }

  const handleMaximumDiscountChange = (index: number, field: keyof MaximumDiscount, value: string) => {
    updateMaximumDiscount(index, { ...watchMaximumDiscounts[index], [field]: value })
    if (field === 'maxSaleUnits' && value === 'custom') {
      setTimeout(() => {
        const customInput = document.getElementById(`custom-max-units-${index}`);
        if (customInput) {
          (customInput as HTMLInputElement).focus();
        }
      }, 0);
    }
  }

  useEffect(() => {
    const fullPrice = watch('fullPrice');
    const interestOption = watch('interestOption');
    const interestPercentage = watch('interestPercentage');

    if (fullPrice !== null) {
      [1, 2, 3, 4, 6, 12].forEach((installment) => {
        if (installment === 1 || interestOption === 'interest free') {
          setValue(`priceInstallments.${installment}`, fullPrice);
        } else {
          const rate = (interestPercentage || 0) / 100;
          const totalWithInterest = calculateCompoundInterest(fullPrice, rate, installment);
          setValue(`priceInstallments.${installment}`, totalWithInterest);
        }
      });
    }
  }, [watch('fullPrice'), watch('interestOption'), watch('interestPercentage'), setValue]);

  const PricingTable = ({ data, confirmed = false }) => (
    <div className="space-y-4">
      <p><strong>Full Price:</strong> {data.fullPrice ? `$${data.fullPrice.toFixed(2)}` : 'N/A'}</p>
      <p><strong>Selected Platform Fee:</strong> {data.platformFee}%</p>
      <p><strong>Interest Option:</strong> {data.interestOption === 'with interest' ? 'With Interest' : 'Interest Free'}</p>
      {data.interestOption === 'with interest' && (
        <p><strong>Interest Percentage:</strong> {data.interestPercentage ? `${data.interestPercentage}%` : 'N/A'}</p>
      )}
      <div>
        <strong>Price Installments:</strong>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Installments</TableHead>
              <TableHead>Total Price</TableHead>
              <TableHead>Price per Installment</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {[1, 2, 3, 4, 6, 12].map((installment) => (
              <TableRow key={installment}>
                <TableCell>{installment}x</TableCell>
                <TableCell>
                  {data.priceInstallments[installment] 
                    ? `$${data.priceInstallments[installment].toFixed(2)}` 
                    : 'N/A'}
                </TableCell>
                <TableCell>
                  {data.priceInstallments[installment] 
                    ? `$${(data.priceInstallments[installment] / installment).toFixed(2)}` 
                    : 'N/A'}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      {data.maximumDiscounts && data.maximumDiscounts.length > 0 && (
        <div>
          <strong>Maximum Automatic Discount:</strong>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Discount</TableHead>
                <TableHead>Period</TableHead>
                <TableHead>Max Sale Units</TableHead>
                <TableHead>Discounted Price</TableHead>
                <TableHead>Final Price (after {data.platformFee}% platform fee)</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.maximumDiscounts
                .filter(item => item.period !== 'no discount')
                .map((item, index) => {
                const discountPercentage = parseFloat(item.discount) / 100;
                const discountedPrice = data.fullPrice * (1 - discountPercentage);
                const finalPrice = calculateFinalPrice(discountedPrice);
                return (
                  <TableRow key={index}>
                    <TableCell>{item.discount}</TableCell>
                    <TableCell>{item.period}</TableCell>
                    <TableCell>{item.maxSaleUnits}</TableCell>
                    <TableCell>${discountedPrice.toFixed(2)}</TableCell>
                    <TableCell>${finalPrice.toFixed(2)}</TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      )}
      <p><strong>Launch Date:</strong> {data.launchDate}</p>
      <p><strong>Closing Date:</strong> {data.closingDate}</p>
      {!confirmed && (
        <p className="text-sm text-gray-600 mt-4">
          Note: The final prices shown are after the platform fee. Please be aware that additional taxes may apply depending on your region.
        </p>
      )}
    </div>
  )

  return (
    <div className="grid md:grid-cols-2 gap-8">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
        <div>
          <Label htmlFor="fullPrice">Full Price (USD)</Label>
          <Input 
            id="fullPrice" 
            type="number" 
            step="0.01" 
            {...register('fullPrice', { 
              setValueAs: v => v === "" ? null : parseFloat(v),
              valueAsNumber: true 
            })} 
            placeholder="Enter full price in USD"
          />
        </div>

        <Collapsible>
          <CollapsibleTrigger className="flex items-center w-full">
            <div className="flex-1 text-left">Price Installments</div>
            <ChevronRight className="h-4 w-4" />
          </CollapsibleTrigger>
          <CollapsibleContent>
            <div className="space-y-4 mb-4">
              <div>
                <Label htmlFor="interestOption">Interest Option</Label>
                <Select
                  onValueChange={(value) => setValue('interestOption', value as 'interest free' | 'with interest')}
                  value={watch('interestOption')}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select interest option" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="interest free">Interest Free</SelectItem>
                    <SelectItem value="with interest">With Interest</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              {watch('interestOption') === 'with interest' && (
                <div>
                  <Label htmlFor="interestPercentage">Interest Percentage (%)</Label>
                  <Input
                    id="interestPercentage"
                    type="number"
                    step="0.01"
                    {...register('interestPercentage', { valueAsNumber: true })}
                    placeholder="Enter interest percentage"
                  />
                </div>
              )}
            </div>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Installments</TableHead>
                  <TableHead>Total Price</TableHead>
                  <TableHead>Price per Installment</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {[1, 2, 3, 4, 6, 12].map((installment) => (
                  <TableRow key={installment}>
                    <TableCell>{installment}x</TableCell>
                    <TableCell>
                      <Input
                        type="number"
                        step="0.01"
                        {...register(`priceInstallments.${installment}`, {
                          valueAsNumber: true,
                        })}
                        readOnly={installment !== 1}
                      />
                    </TableCell>
                    <TableCell>
                      ${(watch(`priceInstallments.${installment}`) / installment).toFixed(2)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CollapsibleContent>
        </Collapsible>

        <Collapsible open={isPlatformRelationshipOpen} onOpenChange={setIsPlatformRelationshipOpen}>
          <CollapsibleTrigger className="flex items-center w-full">
            <div className="flex-1 text-left">Platform Relationship</div>
            {isPlatformRelationshipOpen ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
          </CollapsibleTrigger>
          <CollapsibleContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Platform fee</TableHead>
                  <TableHead>Benefits</TableHead>
                  <TableHead>Select</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {[
                  { fee: 12, benefits: "No marketing for us" },
                  { fee: 25, benefits: "Internal Platform Marketing for us" },
                  { fee: 50, benefits: "Full Marketing for us" },
                ].map((option) => (
                  <TableRow key={option.fee}>
                    <TableCell>{option.fee}%</TableCell>
                    <TableCell>{option.benefits}</TableCell>
                    <TableCell>
                      <input
                        type="radio"
                        name="platformFee"
                        value={option.fee}
                        checked={watch('platformFee') === option.fee}
                        onChange={() => setValue('platformFee', option.fee)}
                      />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CollapsibleContent>
        </Collapsible>

        <Collapsible open={isMaxDiscountOpen} onOpenChange={setIsMaxDiscountOpen}>
          <CollapsibleTrigger className="flex items-center w-full">
            <div className="flex-1 text-left">Maximum Automatic Discount</div>
            {isMaxDiscountOpen ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
          </CollapsibleTrigger>
          <CollapsibleContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Discount</TableHead>
                  <TableHead>Period</TableHead>
                  <TableHead>Max Sale Units</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {maximumDiscountFields.map((field, index) => (
                  <TableRow key={field.id}>
                    <TableCell>{field.discount}</TableCell>
                    <TableCell>
                      <Controller
                        name={`maximumDiscounts.${index}.period` as const}
                        control={control}
                        render={({ field }) => (
                          <Select 
                            onValueChange={(value) => {
                              field.onChange(value)
                              handleMaximumDiscountChange(index, 'period', value)
                            }} 
                            value={field.value}
                          >
                            <SelectTrigger>
                              <SelectValue placeholder="Select period" />
                            </SelectTrigger>
                            <SelectContent>
                              {periodOptions.map((option) => (
                                <SelectItem key={option} value={option}>
                                  {option}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        )}
                      />
                    </TableCell>
                    <TableCell>
                      <Controller
                        name={`maximumDiscounts.${index}.maxSaleUnits` as const}
                        control={control}
                        render={({ field }) => (
                          <>
                            <Select 
                              onValueChange={(value) => {
                                field.onChange(value)
                                handleMaximumDiscountChange(index, 'maxSaleUnits', value)
                              }} 
                              value={field.value}
                            >
                              <SelectTrigger>
                                <SelectValue placeholder="Select max units" />
                              </SelectTrigger>
                              <SelectContent>
                                {maxSaleUnitsOptions.map((option) => (
                                  <SelectItem key={option} value={option}>
                                    {option}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                            {watchMaximumDiscounts[index].maxSaleUnits === 'custom' && (
                              <Input
                                id={`custom-max-units-${index}`}
                                value={field.value === 'custom' ? '' : field.value}
                                onChange={(e) => {
                                  const value = e.target.value;
                                  if (/^\d*$/.test(value)) {
                                    field.onChange(value)
                                    handleMaximumDiscountChange(index, 'maxSaleUnits', value)
                                  }
                                }}
                                onKeyDown={(e) => {
                                  if (e.key === 'Enter') {
                                    e.preventDefault();
                                    (e.target as HTMLInputElement).blur();
                                  }
                                }}
                                placeholder="Enter custom value"
                                type="text"
                                className="mt-2"
                              />
                            )}
                          </>
                        )}
                      />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CollapsibleContent>
        </Collapsible>

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

      <div className="space-y-4">
        <Collapsible open={isPricingConfirmedOpen} onOpenChange={setIsPricingConfirmedOpen}>
          <CollapsibleTrigger className="flex items-center w-full">
            <div className="flex-1 text-left font-bold">Pricing Confirmed</div>
            {isPricingConfirmedOpen ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
          </CollapsibleTrigger>
          <CollapsibleContent>
            <Card className="bg-green-50">
              <CardHeader>
                <CardTitle>Confirmed Pricing</CardTitle>
              </CardHeader>
              <CardContent>
                <PricingTable data={data} confirmed={true} />
              </CardContent>
            </Card>
          </CollapsibleContent>
        </Collapsible>

        <Collapsible open={isPricingPreviewOpen} onOpenChange={setIsPricingPreviewOpen}>
          <CollapsibleTrigger className="flex items-center w-full">
            <div className="flex-1 text-left font-bold">Pricing Preview</div>
            {isPricingPreviewOpen ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
          </CollapsibleTrigger>
          <CollapsibleContent>
            <Card className="bg-yellow-50">
              <CardHeader>
                <CardTitle>Preview Pricing</CardTitle>
              </CardHeader>
              <CardContent>
                <PricingTable data={watch()} />
              </CardContent>
            </Card>
          </CollapsibleContent>
        </Collapsible>
      </div>
    </div>
  )
}

