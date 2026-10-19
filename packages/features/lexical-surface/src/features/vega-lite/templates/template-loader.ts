/**
 * Template Loader for Vega-Lite Templates
 *
 * This module dynamically loads all Vega-Lite templates from the folder structure.
 * Templates are organized hierarchically matching Vega-Lite's official categories.
 */

import type { LucideIcon } from "lucide-react";
import type { VegaSpecRecord } from "../data/vega-spec";

export interface VegaLiteTemplate {
  id: string;
  type: string;
  title: string;
  initialTitle?: string;
  description: string;
  icon: LucideIcon;
  category: string;
  subcategory: string;
  spec: VegaSpecRecord;
  previewImage?: string; // Path to preview image (e.g., "simple-bar.png")
}

export interface TemplateCategory {
  id: string;
  label: string;
  subcategories: TemplateSubcategory[];
}

export interface TemplateSubcategory {
  id: string;
  label: string;
  path: string;
}

/**
 * Template category structure matching Vega-Lite official examples
 */
export const TEMPLATE_CATEGORIES: TemplateCategory[] = [
  {
    id: "starter",
    label: "Starter",
    subcategories: [],
  },
  {
    id: "single-view-plots",
    label: "Single-View Plots",
    subcategories: [
      {
        id: "bar-charts",
        label: "Bar Charts",
        path: "single-view-plots/bar-charts",
      },
      {
        id: "line-charts",
        label: "Line Charts",
        path: "single-view-plots/line-charts",
      },
      {
        id: "histograms-density-plots",
        label: "Histograms, Density Plots & Dot Plots",
        path: "single-view-plots/histograms-density-plots",
      },
      {
        id: "scatter-strip-plots",
        label: "Scatter & Strip Plots",
        path: "single-view-plots/scatter-strip-plots",
      },
      {
        id: "area-streamgraphs",
        label: "Area Charts & Streamgraphs",
        path: "single-view-plots/area-streamgraphs",
      },
      {
        id: "table-plots",
        label: "Table-based Plots",
        path: "single-view-plots/table-plots",
      },
      {
        id: "circular-plots",
        label: "Circular Plots",
        path: "single-view-plots/circular-plots",
      },
      {
        id: "advanced-calculations",
        label: "Advanced Calculations",
        path: "single-view-plots/advanced-calculations",
      },
    ],
  },
  {
    id: "composite-marks",
    label: "Composite Marks",
    subcategories: [
      {
        id: "error-bars-bands",
        label: "Error Bars & Error Bands",
        path: "composite-marks/error-bars-bands",
      },
      {
        id: "box-plots",
        label: "Box Plots",
        path: "composite-marks/box-plots",
      },
    ],
  },
  {
    id: "layered-plots",
    label: "Layered Plots",
    subcategories: [
      {
        id: "labeling-annotation",
        label: "Labeling & Annotation",
        path: "layered-plots/labeling-annotation",
      },
      {
        id: "other-layered",
        label: "Other Layered Plots",
        path: "layered-plots/other-layered",
      },
    ],
  },
  {
    id: "multi-view",
    label: "Multi-View Displays",
    subcategories: [
      {
        id: "faceting",
        label: "Faceting (Trellis Plot / Small Multiples)",
        path: "multi-view/faceting",
      },
      {
        id: "repeat-concat",
        label: "Repeat & Concatenation",
        path: "multi-view/repeat-concat",
      },
    ],
  },
  {
    id: "geographic",
    label: "Geographic Displays",
    subcategories: [{ id: "maps", label: "Maps", path: "geographic" }],
  },
];

/**
 * Dynamically import all templates from the folder structure
 */
async function loadTemplatesFromFolder(): Promise<VegaLiteTemplate[]> {
  const templates: VegaLiteTemplate[] = [];

  // In a real implementation, this would use dynamic imports to load all .ts files
  // For now, we'll manually import the templates we've created

  try {
    const blank = await import("./blank");
    templates.push(blank.default);

    // Single-View Plots - Bar Charts
    const simpleBar = await import("./single-view-plots/bar-charts/simple-bar");
    templates.push(simpleBar.default);

    const groupedBar =
      await import("./single-view-plots/bar-charts/grouped-bar");
    templates.push(groupedBar.default);

    const stackedBar =
      await import("./single-view-plots/bar-charts/stacked-bar");
    templates.push(stackedBar.default);

    const responsiveBar =
      await import("./single-view-plots/bar-charts/responsive-bar");
    templates.push(responsiveBar.default);

    const aggregateBar =
      await import("./single-view-plots/bar-charts/aggregate-bar");
    templates.push(aggregateBar.default);

    const aggregateBarSorted =
      await import("./single-view-plots/bar-charts/aggregate-bar-sorted");
    templates.push(aggregateBarSorted.default);

    const groupedBarMultiMeasure =
      await import("./single-view-plots/bar-charts/grouped-bar-multi-measure");
    templates.push(groupedBarMultiMeasure.default);

    const stackedBarRounded =
      await import("./single-view-plots/bar-charts/stacked-bar-rounded");
    templates.push(stackedBarRounded.default);

    const horizontalStackedBar =
      await import("./single-view-plots/bar-charts/horizontal-stacked-bar");
    templates.push(horizontalStackedBar.default);

    const normalizedStackedBar =
      await import("./single-view-plots/bar-charts/normalized-stacked-bar");
    templates.push(normalizedStackedBar.default);

    const normalizedStackedLabels =
      await import("./single-view-plots/bar-charts/normalized-stacked-labels");
    templates.push(normalizedStackedLabels.default);

    const ganttChart =
      await import("./single-view-plots/bar-charts/gantt-chart");
    templates.push(ganttChart.default);

    const barEncodingColor =
      await import("./single-view-plots/bar-charts/bar-encoding-color");
    templates.push(barEncodingColor.default);

    const layeredBar =
      await import("./single-view-plots/bar-charts/layered-bar");
    templates.push(layeredBar.default);

    const divergingStackedBar =
      await import("./single-view-plots/bar-charts/diverging-stacked-bar");
    templates.push(divergingStackedBar.default);

    const divergingStackedNeutral =
      await import("./single-view-plots/bar-charts/diverging-stacked-neutral");
    templates.push(divergingStackedNeutral.default);

    const barWithLabels =
      await import("./single-view-plots/bar-charts/bar-with-labels");
    templates.push(barWithLabels.default);

    const barLabelOverlays =
      await import("./single-view-plots/bar-charts/bar-label-overlays");
    templates.push(barLabelOverlays.default);

    const barMonthInitials =
      await import("./single-view-plots/bar-charts/bar-month-initials");
    templates.push(barMonthInitials.default);

    const barCenterAligned =
      await import("./single-view-plots/bar-charts/bar-center-aligned");
    templates.push(barCenterAligned.default);

    const barNegativeValues =
      await import("./single-view-plots/bar-charts/bar-negative-values");
    templates.push(barNegativeValues.default);

    const horizontalBarNegative =
      await import("./single-view-plots/bar-charts/horizontal-bar-negative");
    templates.push(horizontalBarNegative.default);

    const barSpacingSaving =
      await import("./single-view-plots/bar-charts/bar-spacing-saving");
    templates.push(barSpacingSaving.default);

    const heatLaneChart =
      await import("./single-view-plots/bar-charts/heat-lane-chart");
    templates.push(heatLaneChart.default);

    // Single-View Plots - Line Charts
    const simpleLine =
      await import("./single-view-plots/line-charts/simple-line");
    templates.push(simpleLine.default);

    const multiLine =
      await import("./single-view-plots/line-charts/multi-line");
    templates.push(multiLine.default);

    // Single-View Plots - Scatter & Strip Plots
    const scatterPlot =
      await import("./single-view-plots/scatter-strip-plots/scatter-plot");
    templates.push(scatterPlot.default);

    // Single-View Plots - Area Charts & Streamgraphs
    const areaChart =
      await import("./single-view-plots/area-streamgraphs/area-chart");
    templates.push(areaChart.default);

    const stackedArea =
      await import("./single-view-plots/area-streamgraphs/stacked-area");
    templates.push(stackedArea.default);

    // Single-View Plots - Histograms, Density Plots
    const histogram =
      await import("./single-view-plots/histograms-density-plots/histogram");
    templates.push(histogram.default);

    // Single-View Plots - Circular Plots
    const pieChart =
      await import("./single-view-plots/circular-plots/pie-chart");
    templates.push(pieChart.default);

    // Single-View Plots - Table-based Plots
    const textTable =
      await import("./single-view-plots/table-plots/text-table");
    templates.push(textTable.default);

    // Single-View Plots - Advanced Calculations
    const heatmap =
      await import("./single-view-plots/advanced-calculations/heatmap");
    templates.push(heatmap.default);

    // Composite Marks - Box Plots
    const boxPlot = await import("./composite-marks/box-plots/box-plot");
    templates.push(boxPlot.default);

    // Composite Marks - Error Bars & Bands
    const errorBars =
      await import("./composite-marks/error-bars-bands/error-bars");
    templates.push(errorBars.default);

    // Layered Plots - Labeling & Annotation
    const annotatedLine =
      await import("./layered-plots/labeling-annotation/annotated-line");
    templates.push(annotatedLine.default);

    // Multi-View Displays - Faceting
    const facetedChart = await import("./multi-view/faceting/faceted-chart");
    templates.push(facetedChart.default);
  } catch (error) {
    console.error("Error loading templates:", error);
  }

  return templates;
}

/**
 * Get all available templates
 */
export async function getAllTemplates(): Promise<VegaLiteTemplate[]> {
  return await loadTemplatesFromFolder();
}

/**
 * Get templates by category
 */
export async function getTemplatesByCategory(
  categoryId: string,
): Promise<VegaLiteTemplate[]> {
  const allTemplates = await getAllTemplates();
  return allTemplates.filter((template) => template.category === categoryId);
}

/**
 * Get templates by subcategory
 */
export async function getTemplatesBySubcategory(
  subcategoryId: string,
): Promise<VegaLiteTemplate[]> {
  const allTemplates = await getAllTemplates();
  return allTemplates.filter(
    (template) => template.subcategory === subcategoryId,
  );
}

/**
 * Get a single template by ID
 */
export async function getTemplateById(
  templateId: string,
): Promise<VegaLiteTemplate | null> {
  const allTemplates = await getAllTemplates();
  return allTemplates.find((template) => template.id === templateId) || null;
}

/**
 * Search templates by keyword
 */
export async function searchTemplates(
  keyword: string,
): Promise<VegaLiteTemplate[]> {
  const allTemplates = await getAllTemplates();
  const lowerKeyword = keyword.toLowerCase();

  return allTemplates.filter(
    (template) =>
      template.title.toLowerCase().includes(lowerKeyword) ||
      template.description.toLowerCase().includes(lowerKeyword) ||
      template.category.toLowerCase().includes(lowerKeyword) ||
      template.subcategory.toLowerCase().includes(lowerKeyword),
  );
}
