"use client"

import { Label } from "@game-guild/ui/components/label"
import { Input } from "@game-guild/ui/components/input"
import { Button } from "@game-guild/ui/components/button"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@game-guild/ui/components/select"
import type { LanguageType, RenameFileDialogProps } from "../types"
import { useEffect } from "react"

export function RenameFileDialog({
  showRenameDialog,
  setShowRenameDialog,
  renameFileName,
  setRenameFileName,
  renameFileLanguage,
  setRenameFileLanguage,
  fileToRename,
  renameFile,
  files,
  getAllowedLanguageTypes,
  getLanguageLabel,
  isPreview = false,
}: RenameFileDialogProps) {
  const fileToRenameObj = fileToRename ? files.find((f) => f.id === fileToRename) : null
  const isLanguageSpecific = false // Allow language changes for all files

  // Auto-update file extension when language changes
  useEffect(() => {
    if (renameFileLanguage && renameFileName) {
      const getExtensionForLanguage = (lang: string): string => {
        const extensionMap: Record<string, string> = {
          javascript: ".js",
          typescript: ".ts",
          python: ".py",
          lua: ".lua",
          c: ".c",
          cpp: ".cpp",
          h: ".h",
          hpp: ".hpp",
          xml: ".xml",
          yaml: ".yaml",
          yml: ".yml",
        }
        return extensionMap[lang] || ""
      }

      // Extract filename without extension
      const lastDotIndex = renameFileName.lastIndexOf(".")
      const nameWithoutExtension = lastDotIndex > 0 ? renameFileName.substring(0, lastDotIndex) : renameFileName
      const newExtension = getExtensionForLanguage(renameFileLanguage)

      if (newExtension) {
        const newFileName = nameWithoutExtension + newExtension
        if (newFileName !== renameFileName) {
          setRenameFileName(newFileName)
        }
      }
    }
  }, [renameFileLanguage, renameFileName, setRenameFileName])

  // Don't render anything in preview mode or when dialog is not shown
  if (!showRenameDialog) return null

  return (
    <div className="absolute inset-0 bg-background/80 backdrop-blur-sm z-50 flex items-center justify-center">
      <div className="bg-background border rounded-lg shadow-lg p-4 w-80">
        <h3 className="text-lg font-medium mb-4">Rename File</h3>
        <div className="space-y-4">
          <div>
            <Label htmlFor="rename-file">New File Name</Label>
            <Input
              id="rename-file"
              value={renameFileName}
              onChange={(e) => setRenameFileName(e.target.value)}
              placeholder="e.g. utils.js"
              className="mt-1"
            />
          </div>
          <div>
            <Label htmlFor="rename-file-language">Language</Label>
            <Select<LanguageType> value={renameFileLanguage} onValueChange={(value) => value !== null && setRenameFileLanguage(value)}>
              <SelectTrigger id="rename-file-language" className="mt-1">
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
            <Button variant="outline" onClick={() => setShowRenameDialog(false)}>
              Cancel
            </Button>
            <Button onClick={renameFile}>Rename</Button>
          </div>
        </div>
      </div>
    </div>
  )
}
