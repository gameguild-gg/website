"use client"

import { Button } from "@game-guild/ui/components/button"
import { Label } from "@game-guild/ui/components/label"
import { Switch } from "@game-guild/ui/components/switch"
import { useCallback, useEffect, useRef, useState } from "react"

export interface LanguageSettingsDialogProps {
  showLanguagesDialog: boolean
  setShowLanguagesDialog: (show: boolean) => void
  allowedLanguages: Record<string, boolean>
  setAllowedLanguages: (langs: Record<string, boolean> | ((prev: Record<string, boolean>) => Record<string, boolean>)) => void
  selectedLanguage: string
  setSelectedLanguage: (lang: string) => void
  getLanguageLabel: (lang: string) => string
  updateSourceCode: (data: {
    allowedLanguages: Record<string, boolean>
    isAutocompleteEnabled: boolean
    activeEnvironments: Record<string, boolean>
    initialFileLanguage?: string
    selectedLanguage?: string
  }) => void
  isAutocompleteEnabled: boolean
  setIsAutocompleteEnabled: (enabled: boolean) => void
  isPreview?: boolean
  isInitialSetup?: boolean
  activeEnvironments: Record<string, boolean>
  setActiveEnvironments: (envs: Record<string, boolean> | ((prev: Record<string, boolean>) => Record<string, boolean>)) => void
}

export function LanguageSettingsDialog({
  showLanguagesDialog,
  setShowLanguagesDialog,
  allowedLanguages,
  setAllowedLanguages,
  selectedLanguage,
  setSelectedLanguage,
  getLanguageLabel,
  updateSourceCode,
  isAutocompleteEnabled,
  setIsAutocompleteEnabled,
  isPreview = false,
  isInitialSetup = false,
  activeEnvironments,
  setActiveEnvironments,
}: LanguageSettingsDialogProps) {
  // Tab state
  const [activeTab, setActiveTab] = useState<"environment" | "files" | "settings">("environment")
  const [isResetConfirmOpen, setIsResetConfirmOpen] = useState(false)

  // Ref to store initial allowed languages for comparison
  const initialAllowedLanguagesRef = useRef<Record<string, boolean> | null>(null)

  // Ref to track if the dialog has been opened before
  const wasDialogOpenBefore = useRef(false)

  // Estado separado para controlar ambientes ativos (independente dos arquivos)
  // const [activeEnvironments, setActiveEnvironments] = useState<Record<string, boolean>>({
  //   javascript: false,
  //   web: false,
  //   typescript: false,
  //   python: false,
  //   lua: false,
  //   cpp: false,
  //   c: false,
  // })

  // Define language groups for each environment
  const jsEnvLangs = ["javascript"]
  const webEnvLangs = ["javascript", "html", "css"]
  const tsEnvLangs = ["typescript", "javascript"]
  const pythonEnvLangs = ["python"]
  const luaEnvLangs = ["lua"]
  const cppEnvLangs = ["cpp", "cppheader", "cheader"]
  const cEnvLangs = ["c", "cheader"]

  // Helper function to get the main language for each environment
  const getMainLanguageForEnvironment = (envName: string): string => {
    switch (envName) {
      case "javascript":
        return "javascript"
      case "web":
        return "javascript" // JS é a linguagem principal mesmo no ambiente web
      case "typescript":
        return "typescript"
      case "python":
        return "python"
      case "lua":
        return "lua"
      case "cpp":
        return "cpp"
      case "c":
        return "c"
      default:
        return "txt"
    }
  }

  // All environment language groups
  const allEnvironments = {
    javascript: jsEnvLangs,
    web: webEnvLangs,
    typescript: tsEnvLangs,
    python: pythonEnvLangs,
    lua: luaEnvLangs,
    cpp: cppEnvLangs,
    c: cEnvLangs,
  }

  // Reset to first tab when dialog opens
  useEffect(() => {
    if (showLanguagesDialog && !wasDialogOpenBefore.current) {
      setActiveTab("environment")
      wasDialogOpenBefore.current = true

      // Store initial settings for comparison
      if (allowedLanguages) {
        initialAllowedLanguagesRef.current = { ...allowedLanguages }
      }
    } else if (!showLanguagesDialog) {
      // Reset state when dialog is closed
      wasDialogOpenBefore.current = false
    }
  }, [showLanguagesDialog, allowedLanguages])

  // Helper function to check if at least one language would remain enabled
  const wouldAtLeastOneLanguageRemainEnabled = useCallback(
    (languageToToggle: string, newState: boolean) => {
      if (newState) return true
      if (!allowedLanguages) return false

      return Object.entries(allowedLanguages).some(([lang, isEnabled]) => lang !== languageToToggle && isEnabled)
    },
    [allowedLanguages],
  )

  // Helper function to check if any settings have changed
  const haveSettingsChanged = useCallback(() => {
    if (!initialAllowedLanguagesRef.current || !allowedLanguages) return false

    for (const lang in allowedLanguages) {
      if (initialAllowedLanguagesRef.current[lang] !== allowedLanguages[lang]) {
        return true
      }
    }

    return false
  }, [allowedLanguages])

  // Function to reset only the current tab
  const resetCurrentTab = useCallback(() => {
    try {
      if (activeTab === "environment") {
        // Reset environments
        setActiveEnvironments({
          javascript: false,
          web: false,
          typescript: false,
          python: false,
          lua: false,
          cpp: false,
          c: false,
        })

        // Reset all languages to false, except txt
        if (setAllowedLanguages && allowedLanguages) {
          setAllowedLanguages((prev: Record<string, boolean>) => {
            const newState = { ...prev }
            Object.keys(newState).forEach((lang) => {
              newState[lang] = lang === "txt"
            })
            return newState
          })
        }

        // If the selected language is not txt, change to txt
        if (selectedLanguage !== "txt" && setSelectedLanguage) {
          setSelectedLanguage("txt")
        }
      } else if (activeTab === "files") {
        // Reset all languages to false, except txt
        if (setAllowedLanguages && allowedLanguages) {
          setAllowedLanguages((prev: Record<string, boolean>) => {
            const newState = { ...prev }
            Object.keys(newState).forEach((lang) => {
              newState[lang] = lang === "txt"
            })
            return newState
          })
        }

        // If the selected language is not txt, change to txt
        if (selectedLanguage !== "txt" && setSelectedLanguage) {
          setSelectedLanguage("txt")
        }
      } else if (activeTab === "settings") {
        // Reset autocomplete settings to default (true)
        if (setIsAutocompleteEnabled) {
          setIsAutocompleteEnabled(true)
        }
      }

      // Close the confirmation dialog
      setIsResetConfirmOpen(false)
    } catch (error) {
      console.error("Error resetting tab:", error)
      setIsResetConfirmOpen(false)
    }
  }, [
    activeTab,
    setAllowedLanguages,
    selectedLanguage,
    setSelectedLanguage,
    setIsAutocompleteEnabled,
    allowedLanguages,
  ])

  // Adicione este useEffect após os outros useEffects existentes
  useEffect(() => {
    // Inicializa as linguagens como desabilitadas por padrão (exceto txt)
    if (isInitialSetup && allowedLanguages) {
      const initialLanguages = Object.keys(allowedLanguages).reduce(
        (acc, lang) => {
          acc[lang] = lang === "txt" // Apenas txt fica habilitado por padrão
          return acc
        },
        {} as Record<string, boolean>,
      )

      setAllowedLanguages(initialLanguages)

      // Define txt como linguagem selecionada se não houver uma selecionada
      if (!selectedLanguage || selectedLanguage === "") {
        setSelectedLanguage("txt")
      }
    }
  }, [isInitialSetup, setAllowedLanguages, setSelectedLanguage])

  // Don't render anything in preview mode or when dialog is not shown
  if (isPreview || !showLanguagesDialog) return null

  // Don't render if required props are missing
  if (!allowedLanguages || !setAllowedLanguages) return null

  return (
    <div
      className="absolute inset-0 bg-background/80 backdrop-blur-sm z-50 flex items-center justify-center"
      onClick={() => setShowLanguagesDialog(false)}
    >
      <div
        className="bg-background border rounded-lg shadow-lg p-3 w-[450px] flex flex-col"
        onClick={(e) => e.stopPropagation()}
      >
        <h3 className="text-lg font-medium mb-2">
          {isInitialSetup ? "Initial Language Configuration" : "Update Language Settings"}
        </h3>
        {isInitialSetup && (
          <p className="text-sm text-muted-foreground mb-3">
            Configure the available languages for this code environment. This configuration can be changed later.
          </p>
        )}
        {!isInitialSetup && (
          <p className="text-sm text-muted-foreground mb-3">Update language settings for this code environment.</p>
        )}

        {/* Tabs */}
        <div className="mb-3">
          <div className="flex relative">
            {/* Tab buttons with step indicators */}
            <button
              className={`relative z-10 px-4 py-2 font-medium text-sm flex items-center ${activeTab === "environment"
                ? "text-primary"
                : activeTab === "files" || activeTab === "settings"
                  ? "text-muted-foreground"
                  : "text-muted-foreground"
                }`}
              onClick={() => setActiveTab("environment")}
            >
              <div
                className={`flex items-center justify-center w-6 h-6 rounded-full mr-2 ${activeTab === "environment" ? "bg-primary text-white" : "bg-gray-200 text-gray-600"
                  }`}
              >
                1
              </div>
              Environment
            </button>

            {/* Arrow connector */}
            <div className="flex items-center text-gray-400 mx-1">→</div>

            <button
              className={`relative z-10 px-4 py-2 font-medium text-sm flex items-center ${activeTab === "files"
                ? "text-primary"
                : activeTab === "settings"
                  ? "text-muted-foreground"
                  : "text-muted-foreground"
                }`}
              onClick={() => setActiveTab("files")}
            >
              <div
                className={`flex items-center justify-center w-6 h-6 rounded-full mr-2 ${activeTab === "files" ? "bg-primary text-white" : "bg-gray-200 text-gray-600"
                  }`}
              >
                2
              </div>
              Files
            </button>

            {/* Arrow connector */}
            <div className="flex items-center text-gray-400 mx-1">→</div>

            <button
              className={`relative z-10 px-4 py-2 font-medium text-sm flex items-center ${activeTab === "settings" ? "text-primary" : "text-muted-foreground"
                }`}
              onClick={() => setActiveTab("settings")}
            >
              <div
                className={`flex items-center justify-center w-6 h-6 rounded-full mr-2 ${activeTab === "settings" ? "bg-primary text-white" : "bg-gray-200 text-gray-600"
                  }`}
              >
                3
              </div>
              Settings
            </button>
          </div>
        </div>

        {/* Content container with fixed height */}
        <div className="h-[300px] overflow-y-auto mb-3">
          {activeTab === "environment" && (
            <div className="grid grid-cols-1 gap-2">
              {/* Development Environments Section */}
              <div className="border rounded-md p-2">
                <div className="grid grid-cols-1 gap-3">
                  {/* JavaScript Environment */}
                  <div className="border-b pb-2">
                    <div className="flex items-center justify-between mb-2">
                      <Label
                        htmlFor="env-javascript"
                        className={`text-sm font-medium ${activeEnvironments.javascript ? "font-bold text-primary" : ""
                          }`}
                      >
                        JavaScript Environment (Pure JS)
                      </Label>
                      <Switch
                        id="env-javascript"
                        checked={activeEnvironments.javascript}
                        onCheckedChange={(checked) => {
                          try {
                            if (checked) {
                              // Desativa todos os outros ambientes
                              setActiveEnvironments({
                                javascript: true,
                                web: false,
                                typescript: false,
                                python: false,
                                lua: false,
                                cpp: false,
                                c: false,
                              })

                              // Desativa todas as linguagens primeiro
                              setAllowedLanguages((prev: Record<string, boolean>) => {
                                const newState = { ...prev }
                                Object.keys(newState).forEach((lang) => {
                                  newState[lang] = lang === "txt"
                                })
                                // Ativa apenas as linguagens do ambiente JavaScript
                                jsEnvLangs.forEach((lang) => {
                                  newState[lang] = true
                                })
                                return newState
                              })

                              // Define a linguagem principal como selecionada
                              const mainLanguage = getMainLanguageForEnvironment("javascript")
                              if (setSelectedLanguage) {
                                setSelectedLanguage(mainLanguage)
                              }
                            } else {
                              // Se está desmarcando, volta para o estado padrão (apenas txt)
                              setActiveEnvironments({
                                javascript: false,
                                web: false,
                                typescript: false,
                                python: false,
                                lua: false,
                                cpp: false,
                                c: false,
                              })

                              setAllowedLanguages((prev: Record<string, boolean>) => {
                                const newState = { ...prev }
                                Object.keys(newState).forEach((lang) => {
                                  newState[lang] = lang === "txt"
                                })
                                return newState
                              })

                              // Volta para txt quando desmarca
                              if (setSelectedLanguage) {
                                setSelectedLanguage("txt")
                              }
                            }
                          } catch (error) {
                            console.error("Error toggling JavaScript environment:", error)
                          }
                        }}
                      />
                    </div>
                  </div>

                  {/* Web Environment */}
                  <div className="border-b pb-2">
                    <div className="flex items-center justify-between mb-2">
                      <Label
                        htmlFor="env-web"
                        className={`text-sm font-medium ${activeEnvironments.web ? "font-bold text-primary" : ""}`}
                      >
                        Web Environment (HTML, CSS, JS)
                      </Label>
                      <Switch
                        id="env-web"
                        checked={activeEnvironments.web}
                        onCheckedChange={(checked) => {
                          try {
                            if (checked) {
                              setActiveEnvironments({
                                javascript: false,
                                web: true,
                                typescript: false,
                                python: false,
                                lua: false,
                                cpp: false,
                                c: false,
                              })

                              setAllowedLanguages((prev: Record<string, boolean>) => {
                                const newState = { ...prev }
                                Object.keys(newState).forEach((lang) => {
                                  newState[lang] = lang === "txt"
                                })
                                webEnvLangs.forEach((lang) => {
                                  newState[lang] = true
                                })
                                return newState
                              })

                              // Define a linguagem principal como selecionada
                              const mainLanguage = getMainLanguageForEnvironment("web")
                              if (setSelectedLanguage) {
                                setSelectedLanguage(mainLanguage)
                              }
                            } else {
                              setActiveEnvironments({
                                javascript: false,
                                web: false,
                                typescript: false,
                                python: false,
                                lua: false,
                                cpp: false,
                                c: false,
                              })

                              setAllowedLanguages((prev: Record<string, boolean>) => {
                                const newState = { ...prev }
                                Object.keys(newState).forEach((lang) => {
                                  newState[lang] = lang === "txt"
                                })
                                return newState
                              })

                              // Volta para txt quando desmarca
                              if (setSelectedLanguage) {
                                setSelectedLanguage("txt")
                              }
                            }
                          } catch (error) {
                            console.error("Error toggling Web environment:", error)
                          }
                        }}
                      />
                    </div>
                  </div>

                  {/* TypeScript Environment */}
                  <div className="border-b pb-2">
                    <div className="flex items-center justify-between mb-2">
                      <Label
                        htmlFor="env-typescript"
                        className={`text-sm font-medium ${activeEnvironments.typescript ? "font-bold text-primary" : ""
                          }`}
                      >
                        TypeScript Environment
                      </Label>
                      <Switch
                        id="env-typescript"
                        checked={activeEnvironments.typescript}
                        onCheckedChange={(checked) => {
                          try {
                            if (checked) {
                              setActiveEnvironments({
                                javascript: false,
                                web: false,
                                typescript: true,
                                python: false,
                                lua: false,
                                cpp: false,
                                c: false,
                              })

                              setAllowedLanguages((prev: Record<string, boolean>) => {
                                const newState = { ...prev }
                                Object.keys(newState).forEach((lang) => {
                                  newState[lang] = lang === "txt"
                                })
                                tsEnvLangs.forEach((lang) => {
                                  newState[lang] = true
                                })
                                return newState
                              })

                              // Define a linguagem principal como selecionada
                              const mainLanguage = getMainLanguageForEnvironment("typescript")
                              if (setSelectedLanguage) {
                                setSelectedLanguage(mainLanguage)
                              }
                            } else {
                              setActiveEnvironments({
                                javascript: false,
                                web: false,
                                typescript: false,
                                python: false,
                                lua: false,
                                cpp: false,
                                c: false,
                              })

                              setAllowedLanguages((prev: Record<string, boolean>) => {
                                const newState = { ...prev }
                                Object.keys(newState).forEach((lang) => {
                                  newState[lang] = lang === "txt"
                                })
                                return newState
                              })

                              // Volta para txt quando desmarca
                              if (setSelectedLanguage) {
                                setSelectedLanguage("txt")
                              }
                            }
                          } catch (error) {
                            console.error("Error toggling TypeScript environment:", error)
                          }
                        }}
                      />
                    </div>
                  </div>

                  {/* Python Environment */}
                  <div className="border-b pb-2">
                    <div className="flex items-center justify-between mb-2">
                      <Label
                        htmlFor="env-python"
                        className={`text-sm font-medium ${activeEnvironments.python ? "font-bold text-primary" : ""}`}
                      >
                        Python Environment
                      </Label>
                      <Switch
                        id="env-python"
                        checked={activeEnvironments.python}
                        onCheckedChange={(checked) => {
                          try {
                            if (checked) {
                              setActiveEnvironments({
                                javascript: false,
                                web: false,
                                typescript: false,
                                python: true,
                                lua: false,
                                cpp: false,
                                c: false,
                              })

                              setAllowedLanguages((prev: Record<string, boolean>) => ({
                                ...Object.keys(prev).reduce(
                                  (acc, lang) => {
                                    acc[lang] = lang === "txt" || lang === "python"
                                    return acc
                                  },
                                  {} as Record<string, boolean>,
                                ),
                              }))

                              // Define a linguagem principal como selecionada
                              const mainLanguage = getMainLanguageForEnvironment("python")
                              if (setSelectedLanguage) {
                                setSelectedLanguage(mainLanguage)
                              }
                            } else {
                              setActiveEnvironments({
                                javascript: false,
                                web: false,
                                typescript: false,
                                python: false,
                                lua: false,
                                cpp: false,
                                c: false,
                              })

                              setAllowedLanguages((prev: Record<string, boolean>) => {
                                const newState = { ...prev }
                                Object.keys(newState).forEach((lang) => {
                                  newState[lang] = lang === "txt"
                                })
                                return newState
                              })

                              // Volta para txt quando desmarca
                              if (setSelectedLanguage) {
                                setSelectedLanguage("txt")
                              }
                            }
                          } catch (error) {
                            console.error("Error toggling Python environment:", error)
                          }
                        }}
                      />
                    </div>
                  </div>

                  {/* Lua Environment */}
                  <div className="border-b pb-2">
                    <div className="flex items-center justify-between mb-2">
                      <Label
                        htmlFor="env-lua"
                        className={`text-sm font-medium ${activeEnvironments.lua ? "font-bold text-primary" : ""}`}
                      >
                        Lua Environment
                      </Label>
                      <Switch
                        id="env-lua"
                        checked={activeEnvironments.lua}
                        onCheckedChange={(checked) => {
                          try {
                            if (checked) {
                              setActiveEnvironments({
                                javascript: false,
                                web: false,
                                typescript: false,
                                python: false,
                                lua: true,
                                cpp: false,
                                c: false,
                              })

                              setAllowedLanguages((prev: Record<string, boolean>) => ({
                                ...Object.keys(prev).reduce(
                                  (acc, lang) => {
                                    acc[lang] = lang === "txt" || lang === "lua"
                                    return acc
                                  },
                                  {} as Record<string, boolean>,
                                ),
                              }))

                              // Define a linguagem principal como selecionada
                              const mainLanguage = getMainLanguageForEnvironment("lua")
                              if (setSelectedLanguage) {
                                setSelectedLanguage(mainLanguage)
                              }
                            } else {
                              setActiveEnvironments({
                                javascript: false,
                                web: false,
                                typescript: false,
                                python: false,
                                lua: false,
                                cpp: false,
                                c: false,
                              })

                              setAllowedLanguages((prev: Record<string, boolean>) => {
                                const newState = { ...prev }
                                Object.keys(newState).forEach((lang) => {
                                  newState[lang] = lang === "txt"
                                })
                                return newState
                              })

                              // Volta para txt quando desmarca
                              if (setSelectedLanguage) {
                                setSelectedLanguage("txt")
                              }
                            }
                          } catch (error) {
                            console.error("Error toggling Lua environment:", error)
                          }
                        }}
                      />
                    </div>
                  </div>

                  {/* C++ Environment */}
                  <div className="border-b pb-2">
                    <div className="flex items-center justify-between mb-2">
                      <Label
                        htmlFor="env-cpp"
                        className={`text-sm font-medium ${activeEnvironments.cpp ? "font-bold text-primary" : ""}`}
                      >
                        C++ Environment
                      </Label>
                      <Switch
                        id="env-cpp"
                        checked={activeEnvironments.cpp}
                        onCheckedChange={(checked) => {
                          try {
                            if (checked) {
                              setActiveEnvironments({
                                javascript: false,
                                web: false,
                                typescript: false,
                                python: false,
                                lua: false,
                                cpp: true,
                                c: false,
                              })

                              setAllowedLanguages((prev: Record<string, boolean>) => {
                                const newState = { ...prev }
                                Object.keys(newState).forEach((lang) => {
                                  newState[lang] = lang === "txt"
                                })
                                cppEnvLangs.forEach((lang) => {
                                  newState[lang] = true
                                })
                                return newState
                              })

                              // Define a linguagem principal como selecionada
                              const mainLanguage = getMainLanguageForEnvironment("cpp")
                              if (setSelectedLanguage) {
                                setSelectedLanguage(mainLanguage)
                              }
                            } else {
                              setActiveEnvironments({
                                javascript: false,
                                web: false,
                                typescript: false,
                                python: false,
                                lua: false,
                                cpp: false,
                                c: false,
                              })

                              setAllowedLanguages((prev: Record<string, boolean>) => {
                                const newState = { ...prev }
                                Object.keys(newState).forEach((lang) => {
                                  newState[lang] = lang === "txt"
                                })
                                return newState
                              })

                              // Volta para txt quando desmarca
                              if (setSelectedLanguage) {
                                setSelectedLanguage("txt")
                              }
                            }
                          } catch (error) {
                            console.error("Error toggling C++ environment:", error)
                          }
                        }}
                      />
                    </div>
                  </div>

                  {/* C Environment */}
                  <div className="border-b pb-2">
                    <div className="flex items-center justify-between mb-2">
                      <Label
                        htmlFor="env-c"
                        className={`text-sm font-medium ${activeEnvironments.c ? "font-bold text-primary" : ""}`}
                      >
                        C Environment
                      </Label>
                      <Switch
                        id="env-c"
                        checked={activeEnvironments.c}
                        onCheckedChange={(checked) => {
                          try {
                            if (checked) {
                              setActiveEnvironments({
                                javascript: false,
                                web: false,
                                typescript: false,
                                python: false,
                                lua: false,
                                cpp: false,
                                c: true,
                              })

                              setAllowedLanguages((prev: Record<string, boolean>) => {
                                const newState = { ...prev }
                                Object.keys(newState).forEach((lang) => {
                                  newState[lang] = lang === "txt"
                                })
                                cEnvLangs.forEach((lang) => {
                                  newState[lang] = true
                                })
                                return newState
                              })

                              // Define a linguagem principal como selecionada
                              const mainLanguage = getMainLanguageForEnvironment("c")
                              if (setSelectedLanguage) {
                                setSelectedLanguage(mainLanguage)
                              }
                            } else {
                              setActiveEnvironments({
                                javascript: false,
                                web: false,
                                typescript: false,
                                python: false,
                                lua: false,
                                cpp: false,
                                c: false,
                              })

                              setAllowedLanguages((prev: Record<string, boolean>) => {
                                const newState = { ...prev }
                                Object.keys(newState).forEach((lang) => {
                                  newState[lang] = lang === "txt"
                                })
                                return newState
                              })

                              // Volta para txt quando desmarca
                              if (setSelectedLanguage) {
                                setSelectedLanguage("txt")
                              }
                            }
                          } catch (error) {
                            console.error("Error toggling C environment:", error)
                          }
                        }}
                      />
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Files Tab Content */}
          {activeTab === "files" && (
            <div className="grid grid-cols-1 gap-2">
              {/* Individual Languages Section */}
              <div className="border rounded-md p-2">
                {isInitialSetup && (
                  <p className="text-xs text-muted-foreground mb-2">
                    Individual file types are disabled by default. Enable the ones you need.
                  </p>
                )}
                <div className="grid grid-cols-2 gap-1">
                  {Object.keys(allowedLanguages)
                    .filter((lang) => lang !== "bash" && lang !== "sh")
                    .map((lang) => (
                      <div key={lang} className="flex items-center justify-between">
                        <Label
                          htmlFor={`lang-${lang}`}
                          className={`text-xs ${allowedLanguages[lang] ? "font-bold text-primary" : ""}`}
                        >
                          {getLanguageLabel ? getLanguageLabel(lang) : lang}
                        </Label>
                        <Switch
                          id={`lang-${lang}`}
                          className="scale-75"
                          checked={allowedLanguages[lang]}
                          onCheckedChange={(checked) => {
                            try {
                              if (!checked && !wouldAtLeastOneLanguageRemainEnabled(lang, checked)) {
                                alert("At least one language must remain enabled.")
                                return
                              }

                              setAllowedLanguages((prev: Record<string, boolean>) => {
                                const newState = { ...prev }
                                newState[lang] = checked
                                return newState
                              })

                              if (lang === selectedLanguage && !checked && setSelectedLanguage) {
                                const nextAvailable = Object.entries(allowedLanguages).find(
                                  ([l, isEnabled]) => l !== lang && isEnabled,
                                )
                                if (nextAvailable) {
                                  setSelectedLanguage(nextAvailable[0])
                                }
                              }
                            } catch (error) {
                              console.error("Error toggling language:", error)
                            }
                          }}
                        />
                      </div>
                    ))}
                </div>
              </div>
            </div>
          )}

          {/* Settings Tab Content */}
          {activeTab === "settings" && (
            <div className="grid grid-cols-1 gap-2">
              {/* Autocomplete Toggle Section */}
              <div className="border rounded-md p-2">
                <div className="flex items-center justify-between">
                  <div>
                    <h4 className="text-sm font-medium">Code Autocomplete</h4>
                    <p className="text-xs text-muted-foreground">Enable/disable code suggestions</p>
                  </div>
                  <Switch
                    checked={isAutocompleteEnabled ?? true}
                    onCheckedChange={(checked) => {
                      try {
                        if (setIsAutocompleteEnabled) {
                          setIsAutocompleteEnabled(checked)
                        }
                      } catch (error) {
                        console.error("Error toggling autocomplete:", error)
                      }
                    }}
                    aria-label="Toggle autocomplete"
                  />
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Reset Confirmation Dialog */}
        {isResetConfirmOpen && (
          <div className="absolute inset-0 bg-background/90 backdrop-blur-sm flex items-center justify-center z-10">
            <div className="bg-background border rounded-lg shadow-lg p-4 w-[350px]">
              <h4 className="text-lg font-medium mb-2">Confirm Reset</h4>
              <p className="text-sm text-muted-foreground mb-4">
                Are you sure you want to reset all settings? This action cannot be undone.
              </p>
              <div className="flex justify-end gap-2">
                <Button size="sm" variant="outline" onClick={() => setIsResetConfirmOpen(false)}>
                  Cancel
                </Button>
                <Button size="sm" variant="destructive" onClick={resetCurrentTab}>
                  Reset
                </Button>
              </div>
            </div>
          </div>
        )}

        <div className="flex justify-between mt-auto">
          <div className="flex gap-2">
            <Button
              size="sm"
              variant="outline"
              onClick={() => setIsResetConfirmOpen(true)}
              className="text-destructive border-destructive hover:bg-destructive/10"
            >
              Reset {activeTab === "environment" ? "Environments" : activeTab === "files" ? "Files" : "Settings"}
            </Button>
          </div>

          <div className="flex gap-2">
            {activeTab !== "settings" && (
              <Button
                size="sm"
                variant="outline"
                onClick={() => {
                  try {
                    if (activeTab === "environment") {
                      setActiveTab("files")
                    } else if (activeTab === "files") {
                      setActiveTab("settings")
                    }
                  } catch (error) {
                    console.error("Error changing tab:", error)
                  }
                }}
              >
                Next
              </Button>
            )}
            <Button
              size="sm"
              variant={haveSettingsChanged() ? "default" : "outline"}
              className={haveSettingsChanged() ? "bg-primary text-primary-foreground" : ""}
              onClick={() => {
                try {
                  if (updateSourceCode) {
                    const finalSettings = { ...allowedLanguages }
                    if (!Object.values(finalSettings).some((enabled) => enabled)) {
                      finalSettings.txt = true
                    }

                    // Determina qual ambiente está ativo para definir a linguagem do arquivo inicial
                    const activeEnv = Object.entries(activeEnvironments).find(([_, isActive]) => isActive)
                    const initialFileLanguage = activeEnv
                      ? getMainLanguageForEnvironment(activeEnv[0])
                      : selectedLanguage

                    updateSourceCode({
                      allowedLanguages: finalSettings,
                      isAutocompleteEnabled: isAutocompleteEnabled,
                      activeEnvironments: activeEnvironments,
                      initialFileLanguage: initialFileLanguage, // Nova propriedade para definir a linguagem do arquivo inicial
                      selectedLanguage: selectedLanguage, // Garante que a linguagem selecionada também seja passada
                    })
                  }
                  setShowLanguagesDialog(false)
                } catch (error) {
                  console.error("Error saving and closing dialog:", error)
                  setShowLanguagesDialog(false) // Still close the dialog
                }
              }}
            >
              {isInitialSetup ? "Save & Continue" : "Save & Close"}
            </Button>
          </div>
        </div>
      </div>
    </div>
  )
}
