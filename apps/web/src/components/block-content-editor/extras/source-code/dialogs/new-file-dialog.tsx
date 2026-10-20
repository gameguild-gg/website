"use client"

import { Label } from "@game-guild/ui/components/label"
import { Input } from "@game-guild/ui/components/input"
import { Button } from "@game-guild/ui/components/button"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@game-guild/ui/components/select"
import type { LanguageType, NewFileDialogProps } from "../types"
import { useState } from "react"

// export interface NewFileDialogProps {
//   showFileDialog: boolean;
//   setShowFileDialog: (show: boolean) => void;
//   newFileName: string;
//   setNewFileName: (name: string) => void;
//   newFileLanguage: string;
//   setNewFileLanguage: (lang: string) => void;
//   newFileHasStates: boolean;
//   setNewFileHasStates: (hasStates: boolean) => boolean;
//   addNewFile: () => void;
//   getAllowedLanguageTypes: () => string[];
//   getLanguageLabel: (lang: string) => string;
//   isPreview?: boolean;
// }

export function NewFileDialog({
  showFileDialog,
  setShowFileDialog,
  newFileName,
  setNewFileName,
  newFileLanguage,
  setNewFileLanguage,
  newFileHasStates,
  setNewFileHasStates,
  addNewFile,
  getAllowedLanguageTypes,
  getLanguageLabel,
  isPreview = false,
}: NewFileDialogProps) {
  const [fileNameError, setFileNameError] = useState(false)
  const [testInputs, setTestInputs] = useState<string[]>([""])

  if (!showFileDialog) return null

  return (
    <div className="absolute inset-0 bg-background/80 backdrop-blur-sm z-50 flex items-center justify-center">
      <div className="bg-background border rounded-lg shadow-lg p-4 w-80">
        <h3 className="text-lg font-medium mb-4">Add New File</h3>
        <div className="space-y-4">
          <div>
            <Label htmlFor="file-name">File Name</Label>
            <Input
              id="file-name"
              value={newFileName}
              onChange={(e) => {
                setNewFileName(e.target.value)
                if (e.target.value) setFileNameError(false)
              }}
              placeholder="e.g. utils.js"
              className={`mt-1 ${fileNameError ? "border-red-500 focus-visible:ring-red-500" : ""}`}
            />
            {fileNameError && <p className="text-red-500 text-xs mt-1">File name is required</p>}
          </div>
          <div>
            <Label htmlFor="file-language">Language</Label>
            <Select<LanguageType> value={newFileLanguage} onValueChange={(value) => value !== null && setNewFileLanguage(value)}>
              <SelectTrigger id="file-language" className="mt-1">
                <SelectValue placeholder="Select language" />
              </SelectTrigger>
              <SelectContent>
                {getAllowedLanguageTypes().map((lang) => (
                  <SelectItem key={lang} value={lang}>
                    {getLanguageLabel(lang)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="flex justify-end space-x-2 pt-2">
            <Button variant="outline" onClick={() => setShowFileDialog(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => {
                if (!newFileName.trim()) {
                  setFileNameError(true)
                  return
                }
                addNewFile()
              }}
            >
              Add File
            </Button>
          </div>
        </div>
      </div>
    </div>
  )
}
