export interface MermaidValidationResult {
  isValid: boolean;
  error?: string;
  line?: number;
  column?: number;
}

export class MermaidValidator {
  private static mermaidAPI: any = null;

  static async initialize() {
    if (!this.mermaidAPI) {
      try {
        const mermaid = await import("mermaid");
        this.mermaidAPI = mermaid.default.mermaidAPI;
      } catch (error) {
        console.error("Failed to initialize Mermaid validator:", error);
      }
    }
  }

  static async validateCode(code: string): Promise<MermaidValidationResult> {
    if (!code || code.trim() === "") {
      return { isValid: false, error: "Code cannot be empty" };
    }

    try {
      await this.initialize();

      if (!this.mermaidAPI) {
        return { isValid: false, error: "Mermaid API not available" };
      }

      // Use mermaid parser to validate syntax
      try {
        // Try to parse the diagram
        await this.mermaidAPI.parse(code);
        return { isValid: true };
      } catch (parseError: any) {
        // Extract error information
        const errorMessage = parseError.message || "Unknown parsing error";
        const lineMatch = errorMessage.match(/line (\d+)/i);
        const line = lineMatch ? Number.parseInt(lineMatch[1], 10) : undefined;

        return {
          isValid: false,
          error: this.formatErrorMessage(errorMessage),
          line,
        };
      }
    } catch (error: any) {
      return {
        isValid: false,
        error: `Validation error: ${error.message || "Unknown error"}`,
      };
    }
  }

  private static formatErrorMessage(error: string): string {
    // Clean up common error messages to be more user-friendly
    if (error.includes("Parse error")) {
      if (error.includes("Expecting")) {
        return error.replace(/Parse error on line \d+:/, "Syntax error:");
      }
    }

    if (error.includes("Lexical error")) {
      return "Invalid character or syntax error";
    }

    if (error.includes("Unknown diagram type")) {
      return "Unknown diagram type. Use: flowchart, sequenceDiagram, classDiagram, etc.";
    }

    return error;
  }

  static getCommonErrors(): { pattern: RegExp; message: string }[] {
    return [
      {
        pattern: /^\s*$/,
        message: "Code cannot be empty",
      },
      {
        pattern:
          /^(?!.*(?:flowchart|graph|sequenceDiagram|classDiagram|stateDiagram|erDiagram|journey|gantt|pie|gitgraph|requirementDiagram|architecture|C4Context|timeline|mindmap|xychart-beta|radar-beta|quadrantChart|sankey-beta|treemap-beta|kanban))/i,
        message:
          "Missing diagram type declaration (e.g., flowchart TD, sequenceDiagram, etc.)",
      },
    ];
  }

  static quickValidate(code: string): MermaidValidationResult {
    const commonErrors = this.getCommonErrors();

    for (const { pattern, message } of commonErrors) {
      if (pattern.test(code)) {
        return { isValid: false, error: message };
      }
    }

    for (const line of code.split("\n")) {
      const trimmed = line.trimEnd();
      if (trimmed.endsWith("-->")) {
        return {
          isValid: false,
          error: "Arrow connection is incomplete - missing target node",
        };
      }
    }

    return { isValid: true };
  }
}
