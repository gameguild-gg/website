export function insertInputs(code: string, inputs: any[], language: string, fileIndex: number): string {
  // If no inputs are provided for this file, return the original code
  if (!inputs || !inputs[fileIndex] || inputs[fileIndex].length === 0) {
    return code;
  }

  const fileInputs = inputs[fileIndex];
  const inputString = JSON.stringify(fileInputs);
  const singleInput = fileInputs.length === 1 ? fileInputs[0] : null;

  switch (language) {
    case 'javascript':
    case 'typescript':
      return `// Insert inputs
const inputs = ${singleInput !== null ? JSON.stringify(singleInput) : inputString};

${code}`;

    case 'python':
      return `# Insert inputs
inputs = ${singleInput !== null ? JSON.stringify(singleInput) : inputString}

${code}`;

    case 'ruby':
      return `# Insert inputs
inputs = ${singleInput !== null ? JSON.stringify(singleInput) : inputString}

${code}`;

    case 'rust':
      const rustInputs = singleInput !== null
        ? `"${singleInput}"`
        : fileInputs.map((i: any) => `"${i}"`).join(", ");
      return `// Insert inputs
let inputs = ${singleInput !== null ? rustInputs : `vec![${rustInputs}]`};

${code}`;

    case 'c':
    case 'cpp':
      const cInputs = singleInput !== null
        ? `"${singleInput}"`
        : fileInputs.map((i: any) => `"${i}"`).join(", ");
      return `// Insert inputs
char *inputs[] = {${cInputs}};

${code}`;

    case 'lua':
      return `-- Insert inputs
inputs = {${fileInputs.map((i: any) => `"${i}"`).join(", ")}}

${code}`;

    case 'java':
      return `// Insert inputs
String[] inputs = ${JSON.stringify(fileInputs)};

${code}`;

    case 'cs':
      return `// Insert inputs
string[] inputs = ${JSON.stringify(fileInputs)};

${code}`;

    default:
      // For unsupported languages or no inputs, return the original code
      return code;
  }
}

