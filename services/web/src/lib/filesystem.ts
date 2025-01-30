type Directory = { [key: string]: string | Directory };

class MemFS {
  private root: Directory;

  constructor() {
    this.root = {};
  }

  /**
   * Create a file with content.
   * @param path Path of the file to create.
   * @param content Content of the file.
   */
  createFile(path: string, content: string): void {
    const { parent, target } = this.resolvePath(path);
    if (parent[target]) {
      throw new Error('File or directory already exists');
    }
    parent[target] = content;
  }

  /**
   * Read the content of a file.
   * @param path Path of the file to read.
   * @returns Content of the file.
   */
  readFile(path: string): string {
    const { parent, target } = this.resolvePath(path);
    if (typeof parent[target] !== 'string') {
      throw new Error('File does not exist');
    }
    return parent[target] as string;
  }

  /**
   * Update the content of a file.
   * @param path Path of the file to update.
   * @param content New content for the file.
   */
  updateFile(path: string, content: string): void {
    const { parent, target } = this.resolvePath(path);
    if (typeof parent[target] !== 'string') {
      throw new Error('File does not exist');
    }
    parent[target] = content;
  }

  /**
   * Delete a file or directory.
   * @param path Path of the file or directory to delete.
   */
  delete(path: string): void {
    const { parent, target } = this.resolvePath(path);
    if (!parent[target]) {
      throw new Error('File or directory does not exist');
    }
    delete parent[target];
  }

  /**
   * Create a directory.
   * @param path Path of the directory to create.
   */
  createDirectory(path: string): void {
    const { parent, target } = this.resolvePath(path);
    if (parent[target]) {
      throw new Error('File or directory already exists');
    }
    parent[target] = {};
  }

  /**
   * List the contents of a directory.
   * @param path Path of the directory to list.
   * @returns List of files and directories in the directory.
   */
  listDirectory(path: string): string[] {
    const { parent, target } = this.resolvePath(path);
    const dir = target ? parent[target] : this.root;
    if (typeof dir !== 'object') {
      throw new Error('Directory does not exist');
    }
    return Object.keys(dir);
  }

  // returns a list of all files in the directory a
  listAllFilesRecursive(path: string): { path: string; filename: string }[] {
    const { parent, target } = this.resolvePath(path);
    const dir = target ? parent[target] : this.root;
    if (typeof dir !== 'object') {
      throw new Error('Directory does not exist');
    }

    const files: { path: string; filename: string }[] = [];

    const traverse = (current: Directory, currentPath: string) => {
      for (const [key, value] of Object.entries(current)) {
        if (typeof value === 'string') {
          files.push({ path: currentPath, filename: key });
        } else {
          traverse(value as Directory, `${currentPath}/${key}`);
        }
      }
    };

    traverse(dir, path);

    return files;
  }

  /**
   * Resolve the path and return the parent directory and target name.
   * @param path Full path to resolve.
   */
  private resolvePath(path: string): { parent: Directory; target: string } {
    const parts = path.split('/').filter(Boolean);
    if (parts.length === 0) throw new Error('Invalid path');

    const target = parts.pop()!;
    let current: Directory = this.root;

    for (const part of parts) {
      if (typeof current[part] === 'string') {
        throw new Error(`Path contains a file: ${part}`);
      }
      current = current[part] as Directory;
    }

    return { parent: current, target };
  }
}
