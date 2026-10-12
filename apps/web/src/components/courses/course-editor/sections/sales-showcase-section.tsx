'use client';

import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Input } from '@game-guild/ui/components/input';
import { Label } from '@game-guild/ui/components/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@game-guild/ui/components/select';
import { Switch } from '@game-guild/ui/components/switch';
import { Calendar, DollarSign, Plus, Tag, Users, X } from 'lucide-react';
import React from 'react';
import { useCourseEditor } from '../../editor/context/course-editor-provider';

export function SalesShowcaseSection() {
  const {
    state,
    addProduct,
    removeProduct,
    setEnrollmentStatus,
    setMaxEnrollments,
    setEstimatedHours,
    addTag,
    removeTag,
    setStatus,
  } = useCourseEditor();

  const [newTag, setNewTag] = React.useState('');
  const [newProductName, setNewProductName] = React.useState('');
  const [newProductPrice, setNewProductPrice] = React.useState('');

  const handleAddProduct = () => {
    if (newProductName && newProductPrice) {
      addProduct({
        id: Date.now().toString(),
        name: newProductName,
        price: parseFloat(newProductPrice),
        currency: 'USD',
        type: 'course',
      });
      setNewProductName('');
      setNewProductPrice('');
    }
  };

  const handleAddTag = () => {
    if (newTag && !state.tags.includes(newTag)) {
      addTag(newTag);
      setNewTag('');
    }
  };

  const handleTagKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      handleAddTag();
    }
  };

  return (
    <div className="space-y-6">
      {/* Pricing & Products */}
      <div className="space-y-4">
        <Label className="text-sm font-medium flex items-center gap-2">
          <DollarSign className="h-4 w-4" />
          Pricing & Products
        </Label>

        {/* Add New Product */}
        <div className="p-4 bg-muted/50 rounded-lg space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <Input placeholder="Product name" value={newProductName} onChange={(e) => setNewProductName(e.target.value)} />
            <div className="flex gap-2">
              <Input type="number" placeholder="Price" value={newProductPrice} onChange={(e) => setNewProductPrice(e.target.value)} />
              <Button aria-label="Add product" onClick={handleAddProduct} disabled={!newProductName || !newProductPrice}>
                <Plus className="h-4 w-4" />
              </Button>
            </div>
          </div>
        </div>

        {/* Product List */}
        {state.products.length > 0 && (
          <div className="space-y-2">
              {state.products.map((product) => (
              <div key={product.id} className="flex items-center justify-between p-3 bg-background border border-border rounded-lg">
                <div className="flex items-center gap-3">
                  <div>
                    <p className="font-medium">{product.name}</p>
                    <p className="text-sm text-muted-foreground">
                      ${product.price} {product.currency} • {product.type}
                    </p>
                  </div>
                </div>
                <Button variant="ghost" size="sm" onClick={() => removeProduct(product.id)}>
                  <X className="h-4 w-4" />
                </Button>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Enrollment Settings */}
      <div className="space-y-4">
        <Label className="text-sm font-medium flex items-center gap-2">
          <Users className="h-4 w-4" />
          Enrollment Settings
        </Label>

        <div className="space-y-4">
          {/* Enrollment Status */}
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium">Open for Enrollment</p>
              <p className="text-xs text-muted-foreground">Allow new students to enroll</p>
            </div>
            <Switch checked={state.enrollment.isOpen} onCheckedChange={setEnrollmentStatus} />
          </div>

          {/* Max Enrollments */}
          <div className="space-y-2">
            <Label htmlFor="maxEnrollments" className="text-sm">
              Maximum Enrollments (Optional)
            </Label>
            <Input
              id="maxEnrollments"
              type="number"
              min="0"
              placeholder="0 for unlimited"
              value={state.enrollment.maxEnrollments ?? ''}
              onChange={(e) => {
                const parsed = Number.parseInt(e.target.value, 10);
                setMaxEnrollments(Number.isFinite(parsed) && parsed > 0 ? parsed : undefined);
              }}
            />
            <p className="text-xs text-muted-foreground">Use 0 or leave empty for unlimited seats.</p>
          </div>

          {/* Current Enrollments Display */}
          <div className="p-3 bg-muted/50 rounded-lg">
            <p className="text-sm">
              <span className="font-medium">Current Enrollments:</span> {state.enrollment.currentEnrollments}
              {state.enrollment.maxEnrollments && <span className="text-muted-foreground"> / {state.enrollment.maxEnrollments}</span>}
            </p>
          </div>
        </div>
      </div>

      {/* Course Metadata */}
      <div className="space-y-4">
        <Label className="text-sm font-medium flex items-center gap-2">
          <Calendar className="h-4 w-4" />
          Course Details
        </Label>

        {/* Estimated Hours */}
        <div className="space-y-2">
          <Label htmlFor="estimatedHours" className="text-sm">
            Estimated Hours to Complete
          </Label>
          <Input id="estimatedHours" type="number" min="1" max="200" value={state.estimatedHours} onChange={(e) => { setEstimatedHours(parseInt(e.target.value) || 1); }} />
        </div>

        {/* Publishing Status */}
        <div className="space-y-2">
          <Label className="text-sm">Publishing Status</Label>
          <Select value={state.status} onValueChange={(value) => value !== null && setStatus(value)}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="draft">Draft</SelectItem>
              <SelectItem value="published">Published</SelectItem>
              <SelectItem value="archived">Archived</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Tags */}
      <div className="space-y-4">
        <Label className="text-sm font-medium flex items-center gap-2">
          <Tag className="h-4 w-4" />
          Tags
        </Label>

        {/* Add Tag */}
        <div className="flex gap-2">
          <Input placeholder="Add a tag..." value={newTag} onChange={(e) => setNewTag(e.target.value)} onKeyPress={handleTagKeyPress} />
          <Button aria-label="Add tag" onClick={handleAddTag} disabled={!newTag || state.tags.includes(newTag)}>
            <Plus className="h-4 w-4" />
          </Button>
        </div>

        {/* Tag List */}
        {state.tags.length > 0 && (
          <div className="flex flex-wrap gap-2">
            {state.tags.map((tag) => (
              <Badge key={tag} variant="secondary" className="flex items-center gap-1">
                {tag}
                <button onClick={() => removeTag(tag)} className="ml-1 hover:text-red-500">
                  <X className="h-3 w-3" />
                </button>
              </Badge>
            ))}
          </div>
        )}

        <p className="text-xs text-muted-foreground">Tags help students find your course and improve discoverability.</p>
      </div>
    </div>
  );
}
