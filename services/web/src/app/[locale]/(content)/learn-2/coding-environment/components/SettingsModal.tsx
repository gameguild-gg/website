import React, { useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"

interface SettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: (settings: Settings) => void;
  mode: 'light' | 'dark' | 'high-contrast';
  initialSettings: Settings;
}

export interface Settings {
  runSettings: {
    fileExtensions: { [key: string]: 'Open new guide' | 'Internal output' | 'Both' };
    applicationFileName: string;
  };
}

const defaultSettings: Settings = {
  runSettings: {
    fileExtensions: {
      '.html': 'Open new guide',
      '.js': 'Internal output',
      '.jsx': 'Internal output',
      '.ts': 'Internal output',
      '.tsx': 'Internal output',
      '.py': 'Internal output',
      '.java': 'Internal output',
      '.cpp': 'Internal output',
      '.c': 'Internal output',
    },
    applicationFileName: '', // Changed from 'index' to an empty string
  },
};

export default function SettingsModal({ isOpen, onClose, onConfirm, mode, initialSettings }: SettingsModalProps) {
  const [activeMenu, setActiveMenu] = useState('general');
  const [settings, setSettings] = useState<Settings>(initialSettings || defaultSettings);

  const getButtonStyles = (variant: 'primary' | 'secondary') => {
    const baseStyles = 'px-4 py-2 rounded transition-colors duration-200'
    switch (mode) {
      case 'light':
        return variant === 'primary'
          ? `${baseStyles} bg-blue-500 text-white hover:bg-blue-600`
          : `${baseStyles} bg-gray-200 text-gray-800 hover:bg-gray-300`
      case 'dark':
        return variant === 'primary'
          ? `${baseStyles} bg-blue-600 text-white hover:bg-blue-700`
          : `${baseStyles} bg-gray-700 text-gray-200 hover:bg-gray-600`
      case 'high-contrast':
        return variant === 'primary'
          ? `${baseStyles} bg-yellow-300 text-black hover:bg-yellow-400`
          : `${baseStyles} bg-gray-800 text-yellow-300 hover:bg-gray-700 border border-yellow-300`
      default:
        return ''
    }
  }

  const getMenuItemStyles = (isActive: boolean) => {
    const baseStyles = 'px-4 py-2 w-full text-left transition-colors duration-200'
    switch (mode) {
      case 'light':
        return isActive
          ? `${baseStyles} bg-blue-100 text-blue-700`
          : `${baseStyles} hover:bg-gray-100`
      case 'dark':
        return isActive
          ? `${baseStyles} bg-blue-700 text-white`
          : `${baseStyles} hover:bg-gray-700`
      case 'high-contrast':
        return isActive
          ? `${baseStyles} bg-yellow-300 text-black`
          : `${baseStyles} hover:bg-gray-800 text-yellow-300`
      default:
        return baseStyles
    }
  }


  const getInputStyles = () => {
    switch (mode) {
      case 'light':
        return 'bg-white text-gray-900 border-gray-300'
      case 'dark':
        return 'bg-gray-700 text-gray-100 border-gray-600'
      case 'high-contrast':
        return 'bg-black text-yellow-300 border-yellow-300'
      default:
        return ''
    }
  }

  const getSelectStyles = () => {
    switch (mode) {
      case 'light':
        return 'bg-white text-gray-900 border-gray-300'
      case 'dark':
        return 'bg-gray-800 text-gray-100 border-gray-600'
      case 'high-contrast':
        return 'bg-black text-yellow-300 border-yellow-300'
      default:
        return ''
    }
  }

  const getSelectContentStyles = () => {
    switch (mode) {
      case 'light':
        return 'bg-white text-gray-900'
      case 'dark':
        return 'bg-gray-900 text-gray-100'
      case 'high-contrast':
        return 'bg-black text-yellow-300 border border-yellow-300'
      default:
        return ''
    }
  }

  const getDialogStyles = () => {
    switch (mode) {
      case 'light':
        return 'bg-white text-gray-900'
      case 'dark':
        return 'bg-gray-800 text-gray-100'
      case 'high-contrast':
        return 'bg-black text-yellow-300 border-2 border-yellow-300'
      default:
        return ''
    }
  }

  const handleConfirm = () => {
    onConfirm(settings);
  };

  const updateFileExtensionSetting = (extension: string, value: 'Open new guide' | 'Internal output' | 'Both') => {
    setSettings(prev => ({
      ...prev,
      runSettings: {
        ...prev.runSettings,
        fileExtensions: {
          ...prev.runSettings.fileExtensions,
          [extension]: value
        }
      }
    }));
  };

  const updateApplicationFileName = (fileName: string) => {
    setSettings(prev => ({
      ...prev,
      runSettings: {
        ...prev.runSettings,
        applicationFileName: fileName
      }
    }));
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className={`${getDialogStyles()} w-full max-w-3xl h-[70vh] flex flex-col`} aria-describedby="settings-description">
        <DialogHeader>
          <DialogTitle className={mode === 'high-contrast' ? 'text-yellow-300' : ''}>Settings</DialogTitle>
          <DialogDescription id="settings-description" className={mode === 'high-contrast' ? 'text-yellow-200' : ''}>
            Adjust your coding environment settings here.
          </DialogDescription>
        </DialogHeader>
        <div className="flex flex-grow overflow-hidden">
          <div className="w-1/4 border-r pr-4 pt-4">
            <button
              onClick={() => setActiveMenu('general')}
              className={getMenuItemStyles(activeMenu === 'general')}
            >
              General
            </button>
            <button
              onClick={() => setActiveMenu('run')}
              className={getMenuItemStyles(activeMenu === 'run')}
            >
              Run
            </button>
            <button
              onClick={() => setActiveMenu('editor')}
              className={getMenuItemStyles(activeMenu === 'editor')}
            >
              Editor
            </button>
            <button
              onClick={() => setActiveMenu('advanced')}
              className={getMenuItemStyles(activeMenu === 'advanced')}
            >
              Advanced
            </button>
          </div>
          <div className="w-3/4 pl-4 pt-4 overflow-y-auto">
            {activeMenu === 'general' && <div>General Settings Content</div>}
            {activeMenu === 'run' && (
              <div>
                <h3 className="text-lg font-semibold mb-4">Run Settings</h3>
                <div className="space-y-4">
                  <div>
                    <Label htmlFor="applicationFileName">Application File Name (optional)</Label>
                    <Input
                      id="applicationFileName"
                      value={settings.runSettings.applicationFileName}
                      onChange={(e) => updateApplicationFileName(e.target.value)}
                      className={`mt-1 ${getInputStyles()}`}
                      placeholder="Leave empty to use the active file"
                    />
                    <p className={`mt-1 text-sm ${
                      mode === 'light' ? 'text-gray-600' :
                      mode === 'dark' ? 'text-gray-400' :
                      'text-yellow-200'
                    }`}>
                      If left empty, the currently selected file will be used when running the application.
                    </p>
                  </div>
                  <div>
                    <h4 className="text-md font-semibold mb-2">File Extension Settings</h4>
                    {Object.entries(settings.runSettings.fileExtensions).map(([ext, value]) => (
                      <div key={ext} className="flex items-center space-x-2 mb-2">
                        <Label htmlFor={`ext-${ext}`} className="w-20">{ext}</Label>
                        <Select
                          value={value}
                          onValueChange={(newValue: 'Open new guide' | 'Internal output' | 'Both') => updateFileExtensionSetting(ext, newValue)}
                        >
                          <SelectTrigger id={`ext-${ext}`} className={`w-40 ${getSelectStyles()}`}>
                            <SelectValue placeholder="Select option" />
                          </SelectTrigger>
                          <SelectContent className={getSelectContentStyles()}>
                            <SelectItem 
                              value="Open new guide"
                              className={`
                                ${mode === 'light' ? 'text-gray-900 hover:bg-gray-100' : ''}
                                ${mode === 'dark' ? 'text-gray-100 hover:bg-gray-800' : ''}
                                ${mode === 'high-contrast' ? 'text-yellow-300 hover:bg-gray-800' : ''}
                              `}
                            >
                              Open new guide
                            </SelectItem>
                            <SelectItem 
                              value="Internal output"
                              className={`
                                ${mode === 'light' ? 'text-gray-900 hover:bg-gray-100' : ''}
                                ${mode === 'dark' ? 'text-gray-100 hover:bg-gray-800' : ''}
                                ${mode === 'high-contrast' ? 'text-yellow-300 hover:bg-gray-800' : ''}
                              `}
                            >
                              Internal output
                            </SelectItem>
                            <SelectItem 
                              value="Both"
                              className={`
                                ${mode === 'light' ? 'text-gray-900 hover:bg-gray-100' : ''}
                                ${mode === 'dark' ? 'text-gray-100 hover:bg-gray-800' : ''}
                                ${mode === 'high-contrast' ? 'text-yellow-300 hover:bg-gray-800' : ''}
                              `}
                            >
                              Both
                            </SelectItem>
                          </SelectContent>
                        </Select>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            )}
            {activeMenu === 'editor' && <div>Editor Settings Content</div>}
            {activeMenu === 'advanced' && <div>Advanced Settings Content</div>}
          </div>
        </div>
        <DialogFooter className="border-t mt-auto py-2">
          <Button onClick={onClose} className={getButtonStyles('secondary')}>Cancel</Button>
          <Button onClick={handleConfirm} className={getButtonStyles('primary')}>Confirm</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

