import * as d3Dsv from "d3-dsv";
import { isVegaSpecRecord } from "../vega-spec";

/**
 * Processa uma especificação Vega-Lite e substitui URLs data: por dados inline
 */
export function loadCsvDataIntoSpec(
  spec: string,
  dataFiles: Record<string, string> = {},
): unknown {
  try {
    const parsedSpec: unknown = JSON.parse(spec);
    return processDataNode(parsedSpec, dataFiles);
  } catch (error) {
    console.error("Erro ao processar dados no spec:", error);
    return spec;
  }
}

/**
 * Processa recursivamente um nó do spec procurando por propriedades "data"
 */
function processDataNode(node: unknown, dataFiles: Record<string, string>): unknown {
  // Se é um array, processa cada item
  if (Array.isArray(node)) {
    return node.map((item: unknown) => processDataNode(item, dataFiles));
  }
  if (!isVegaSpecRecord(node)) return node;

  // Cria uma cópia do objeto
  const processed: Record<string, unknown> = {};

  for (const [key, value] of Object.entries(node)) {
    // Se encontrou uma propriedade "data" com "url"
    if (
      key === "data" &&
      isVegaSpecRecord(value) &&
      typeof value.url === "string"
    ) {
      const dataValue = value;
      const dataUrl = value.url;

      // Verifica se é uma URL data:
      if (dataUrl.startsWith("data:")) {
        const filename = dataUrl.substring(5); // Remove "data:"

        // Verifica se temos esse arquivo
        if (dataFiles[filename]) {
          const content = dataFiles[filename];

          // Verifica se é CSV
          if (filename.endsWith(".csv")) {
            try {
              // Parse CSV para JSON
              const parsedData = d3Dsv.csvParse(content);

              // Substitui url por values
              const inlineData: Record<string, unknown> = {
                ...dataValue,
                values: parsedData,
              };

              // Remove a propriedade url
              delete inlineData.url;
              processed[key] = inlineData;

              continue;
            } catch (error) {
              console.error(`Erro ao parsear CSV ${filename}:`, error);
              // Mantém a URL original em caso de erro
              processed[key] = value;
              continue;
            }
          }

          // Verifica se é JSON
          if (filename.endsWith(".json")) {
            try {
              // Parse JSON
              const parsedData: unknown = JSON.parse(content);

              // Substitui url por values
              const inlineData: Record<string, unknown> = {
                ...dataValue,
                values: Array.isArray(parsedData) ? parsedData : [parsedData],
              };

              // Remove a propriedade url
              delete inlineData.url;
              processed[key] = inlineData;

              continue;
            } catch (error) {
              console.error(`Erro ao parsear JSON ${filename}:`, error);
              // Mantém a URL original em caso de erro
              processed[key] = value;
              continue;
            }
          }
        }

        console.warn(`Arquivo não encontrado: ${filename}`);
        // Mantém a URL original
        processed[key] = value;
        continue;
      }
    }

    // Para outros casos, processa recursivamente
    processed[key] = processDataNode(value, dataFiles);
  }

  return processed;
}

/**
 * Verifica se um spec contém referências a dados CSV
 */
export function hasDataReferences(spec: string): boolean {
  try {
    return spec.includes('"data:');
  } catch {
    return false;
  }
}

/**
 * Extrai nomes de arquivos CSV ou JSON referenciados no spec
 */
export function extractDataFilenames(spec: string): {
  csv: string[];
  json: string[];
} {
  const csvFiles: string[] = [];
  const jsonFiles: string[] = [];
  const regex = /"data:([^"]+\.(csv|json))"/g;
  let match;

  while ((match = regex.exec(spec)) !== null) {
    const filename = match[1];
    const extension = match[2];

    if (extension === "csv" && filename && !csvFiles.includes(filename)) {
      csvFiles.push(filename);
    } else if (
      extension === "json" &&
      filename &&
      !jsonFiles.includes(filename)
    ) {
      jsonFiles.push(filename);
    }
  }

  return { csv: csvFiles, json: jsonFiles };
}
