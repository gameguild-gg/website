"use client";

import { useRef, useState } from "react";
import {
  AlertCircle,
  BarChart3,
  CheckCircle,
  FileJson,
  LayoutTemplate,
  Save,
} from "lucide-react";
import { Button } from "@game-guild/ui/components/button";
import { Input } from "@game-guild/ui/components/input";
import { Label } from "@game-guild/ui/components/label";
import {
  Select,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@game-guild/ui/components/select";
import { Switch } from "@game-guild/ui/components/switch";
import { FeatureEditorShell } from "../../../shared/ui/feature-editor-shell";
import { FeatureEditorSelectContent } from "../../../shared/ui/feature-editor-select";
import { useFeatureEditorSettings } from "../../../shared/ui/use-feature-editor-settings";
import { ValidationPanel } from "../../../shared/ui/validation-panel";
import { useDarkMode } from "../../../shared/ui/use-dark-mode";
import { ControlledVegaLiteViewer } from "../rendering/controlled-vega-lite-viewer";
import { MonacoVegaLiteEditor } from "./monaco-vega-lite-editor";
import type { VegaLiteData } from "../vega-lite-data";
import { VegaLiteExport } from "./vega-lite-export";
import { VegaLiteManager } from "./vega-lite-manager";
import { VegaLiteTemplateSelector } from "./vega-lite-template-selector";
import {
  VegaLiteValidator,
  type VegaLiteValidationResult,
} from "./vega-lite-validator";
import {
  AVAILABLE_THEMES,
  getThemePair,
  THEME_DESCRIPTIONS,
  THEME_MODE_DESCRIPTIONS,
  type ThemeMode,
  type VegaThemeBase,
} from "../theme/vega-theme-helper";

const DEFAULT_SPEC = JSON.stringify(
  {
    $schema: "https://vega.github.io/schema/vega-lite/v6.json",
    data: {
      values: [
        { category: "A", value: 28 },
        { category: "B", value: 55 },
      ],
    },
    mark: "bar",
    encoding: {
      x: { field: "category", type: "nominal" },
      y: { field: "value", type: "quantitative" },
    },
  },
  null,
  2,
);

const EMPTY_VEGA_DATA: VegaLiteData = {
  spec: DEFAULT_SPEC,
  title: "",
  caption: "",
  size: 100,
  theme: "default",
  themeMode: "system",
  layout: "rectangular",
  attachments: {},
};

export function VegaLiteEditor({
  initialData,
  onSave,
  onCancel,
}: {
  initialData?: VegaLiteData;
  onSave: (data: VegaLiteData) => void;
  onCancel: () => void;
}) {
  const isDarkMode = useDarkMode();
  const settings = useFeatureEditorSettings("vega-lite");
  const initial = {
    ...EMPTY_VEGA_DATA,
    ...initialData,
    spec: initialData?.spec ?? DEFAULT_SPEC,
  };
  const [data, setData] = useState<VegaLiteData>(initial);
  const dataRef = useRef(data);
  const [previewData, setPreviewData] = useState<VegaLiteData>(initial);
  const [previewKey, setPreviewKey] = useState(0);
  const [showTemplates, setShowTemplates] = useState(!initialData);
  const [autoUpdate, setAutoUpdate] = useState(true);
  const [validation, setValidation] = useState<VegaLiteValidationResult>({
    isValid: true,
  });
  const [errorCollapsed, setErrorCollapsed] = useState(false);

  const commitPreview = (next: VegaLiteData) => {
    setPreviewData(next);
    setPreviewKey((key) => key + 1);
  };

  const updateData = (
    patch: Partial<VegaLiteData>,
    updatePreviewImmediately = true,
  ) => {
    const next = { ...dataRef.current, ...patch };
    dataRef.current = next;
    setData(next);
    if (autoUpdate && updatePreviewImmediately && validation.isValid) {
      commitPreview(next);
    }
  };

  const handleValidation = (result: VegaLiteValidationResult) => {
    setValidation(result);
    setErrorCollapsed(result.isValid);
    if (result.isValid && autoUpdate) commitPreview(dataRef.current);
  };

  const updatePreview = async () => {
    const result = await VegaLiteValidator.validateSpec(dataRef.current.spec);
    handleValidation(result);
    if (result.isValid) commitPreview(dataRef.current);
  };

  const previewTheme = getThemePair(
    (previewData.theme ?? "default") as VegaThemeBase,
    (previewData.themeMode ?? "system") as ThemeMode,
  );

  return (
    <FeatureEditorShell
      settings={settings}
      title="Vega-Lite Chart Editor"
      icon={<BarChart3 className="h-5 w-5 text-blue-500" />}
      onClose={onCancel}
      headerMeta={
        <div className="flex items-center gap-2 text-sm">
          {validation.isValid ? (
            <>
              <CheckCircle className="h-4 w-4 text-emerald-500" />
              <span className="text-emerald-600 dark:text-emerald-400">
                Valid
              </span>
            </>
          ) : (
            <>
              <AlertCircle className="h-4 w-4 text-red-500" />
              <span className="text-red-600 dark:text-red-400">Invalid</span>
            </>
          )}
        </div>
      }
      headerActions={
        !showTemplates && (
          <>
            <VegaLiteManager
              attachments={data.attachments ?? {}}
              onAttachmentsChange={(attachments) => updateData({ attachments })}
            />
            <VegaLiteExport
              spec={previewData.spec}
              themeLight={previewTheme.themeLight}
              themeDark={previewTheme.themeDark}
              layout={previewData.layout}
              title={previewData.title}
              isValid={validation.isValid}
              attachments={previewData.attachments}
            />
            <Button
              variant="outline"
              size="sm"
              onClick={() => setShowTemplates(true)}
            >
              <LayoutTemplate className="mr-2 h-4 w-4" />
              <span className="hidden sm:inline">Templates</span>
            </Button>
          </>
        )
      }
      footer={
        showTemplates ? undefined : (
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <div className="min-w-0 flex-1">
              <Label htmlFor="vega-caption">Caption</Label>
              <Input
                id="vega-caption"
                value={data.caption ?? ""}
                onChange={(event) =>
                  updateData({ caption: event.target.value })
                }
                placeholder="Optional chart caption"
                className="mt-1"
              />
            </div>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={onCancel}>
                Cancel
              </Button>
              <Button
                onClick={() => onSave(dataRef.current)}
                disabled={!validation.isValid || !data.spec.trim()}
              >
                <Save className="mr-2 h-4 w-4" />
                Save Chart
              </Button>
            </div>
          </div>
        )
      }
      bodyClassName="overflow-y-auto lg:overflow-hidden"
    >
      {showTemplates ? (
        <VegaLiteTemplateSelector
          onCancel={() => (initialData ? setShowTemplates(false) : onCancel())}
          onSelect={(template) => {
            updateData(
              {
                spec: template.spec,
                title: template.title ?? dataRef.current.title,
              },
              false,
            );
            setShowTemplates(false);
          }}
        />
      ) : (
        <>
          <div className="flex shrink-0 flex-wrap items-end gap-3 border-b bg-gray-50 p-3 dark:border-gray-800 dark:bg-gray-900">
            <div className="min-w-44 flex-1 sm:max-w-64">
              <Label htmlFor="vega-title">Title</Label>
              <Input
                id="vega-title"
                value={data.title ?? ""}
                onChange={(event) => updateData({ title: event.target.value })}
                placeholder="Optional title"
                className="mt-1"
              />
            </div>
            <div className="w-40">
              <Label>Theme</Label>
              <Select
                value={data.theme ?? "default"}
                onValueChange={(theme) =>
                  updateData({ theme: theme as VegaThemeBase })
                }
              >
                <SelectTrigger className="mt-1 w-full">
                  <SelectValue />
                </SelectTrigger>
                <FeatureEditorSelectContent className="max-h-72">
                  {AVAILABLE_THEMES.map((theme) => (
                    <SelectItem key={theme} value={theme}>
                      {THEME_DESCRIPTIONS[theme]}
                    </SelectItem>
                  ))}
                </FeatureEditorSelectContent>
              </Select>
            </div>
            <div className="w-40">
              <Label>Theme mode</Label>
              <Select
                value={data.themeMode ?? "system"}
                onValueChange={(themeMode) =>
                  updateData({ themeMode: themeMode as ThemeMode })
                }
              >
                <SelectTrigger className="mt-1 w-full">
                  <SelectValue />
                </SelectTrigger>
                <FeatureEditorSelectContent>
                  {Object.entries(THEME_MODE_DESCRIPTIONS).map(
                    ([mode, description]) => (
                      <SelectItem key={mode} value={mode}>
                        {description.label}
                      </SelectItem>
                    ),
                  )}
                </FeatureEditorSelectContent>
              </Select>
            </div>
            <div className="w-36">
              <Label>Layout</Label>
              <Select
                value={data.layout ?? "rectangular"}
                onValueChange={(layout) =>
                  updateData({ layout: layout as "square" | "rectangular" })
                }
              >
                <SelectTrigger className="mt-1 w-full">
                  <SelectValue />
                </SelectTrigger>
                <FeatureEditorSelectContent>
                  <SelectItem value="rectangular">Rectangular</SelectItem>
                  <SelectItem value="square">Square</SelectItem>
                </FeatureEditorSelectContent>
              </Select>
            </div>
            <div className="w-24">
              <Label htmlFor="vega-size">Width (%)</Label>
              <Input
                id="vega-size"
                type="number"
                min={20}
                max={100}
                value={data.size ?? 100}
                onChange={(event) =>
                  updateData({
                    size: Math.max(
                      20,
                      Math.min(100, Number(event.target.value)),
                    ),
                  })
                }
                className="mt-1"
              />
            </div>
            <div className="flex h-9 items-center gap-2 pb-0.5">
              <Switch
                id="vega-auto-update"
                checked={autoUpdate}
                onCheckedChange={(checked) => {
                  setAutoUpdate(checked);
                  if (checked && validation.isValid)
                    commitPreview(dataRef.current);
                }}
              />
              <Label htmlFor="vega-auto-update">Live preview</Label>
            </div>
            {!autoUpdate && (
              <Button variant="outline" onClick={() => void updatePreview()}>
                Update preview
              </Button>
            )}
          </div>

          <div className="flex min-h-0 flex-1 flex-col lg:flex-row">
            <section className="flex min-h-[320px] min-w-0 flex-1 flex-col border-b lg:min-h-0 lg:border-b-0 lg:border-r dark:border-gray-800">
              <div className="flex h-11 shrink-0 items-center gap-2 border-b bg-gray-50 px-4 text-sm font-medium dark:border-gray-800 dark:bg-gray-900">
                <FileJson className="h-4 w-4 text-blue-500" /> Vega-Lite
                specification
              </div>
              <div className="min-h-0 flex-1 bg-gray-950 p-2">
                <MonacoVegaLiteEditor
                  value={data.spec}
                  onChange={(spec) => updateData({ spec: spec ?? "" }, false)}
                  onValidationChange={handleValidation}
                  theme={isDarkMode ? "dark" : "light"}
                  options={settings.editor}
                />
              </div>
              {!validation.isValid && (
                <ValidationPanel
                  error={validation.error ?? "Invalid Vega-Lite specification"}
                  collapsed={errorCollapsed}
                  onCollapsedChange={setErrorCollapsed}
                />
              )}
            </section>

            <section className="flex min-h-[360px] min-w-0 flex-1 flex-col lg:min-h-0">
              <div className="flex h-11 shrink-0 items-center justify-between gap-2 border-b bg-gray-50 px-4 text-sm font-medium dark:border-gray-800 dark:bg-gray-900">
                <span className="flex items-center gap-2">
                  <BarChart3 className="h-4 w-4 text-blue-500" /> Preview
                </span>
              </div>
              <div className="min-h-0 flex-1 overflow-auto bg-white p-4 dark:bg-gray-950">
                <ControlledVegaLiteViewer
                  spec={previewData.spec}
                  layout={previewData.layout}
                  themeLight={previewTheme.themeLight}
                  themeDark={previewTheme.themeDark}
                  title={previewData.title}
                  caption={previewData.caption}
                  size={previewData.size}
                  showControls
                  allowFullscreen={false}
                  className="min-h-full"
                  attachments={previewData.attachments}
                  updateTrigger={previewKey}
                />
              </div>
            </section>
          </div>
        </>
      )}
    </FeatureEditorShell>
  );
}
