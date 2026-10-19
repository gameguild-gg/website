export function getImportErrorMessage(error: unknown, fallback: string): string {
  if (typeof error === "object" && error !== null && "message" in error && typeof error.message === "string") {
    return error.message || fallback
  }
  return fallback
}

// Recursively read every batch from the browser's File and Directory Entries API.
export async function readEntriesAsFiles(entry: FileSystemEntry, pathPrefix = ""): Promise<File[]> {
  if (entry.isFile) {
    const fileEntry = entry as FileSystemFileEntry
    return new Promise<File[]>((resolve, reject) => {
      fileEntry.file((file) => {
        // Preserve the relative path expected by ProjectImporter.importFromFolder.
        try {
          Object.defineProperty(file, "webkitRelativePath", {
            value: `${pathPrefix}${file.name}`,
            configurable: true,
          })
        } catch {
          /* non-fatal */
        }
        resolve([file])
      }, reject)
    })
  }
  if (entry.isDirectory) {
    const directoryEntry = entry as FileSystemDirectoryEntry
    const reader = directoryEntry.createReader()
    const allEntries: FileSystemEntry[] = []
    while (true) {
      const batch = await new Promise<FileSystemEntry[]>((resolve, reject) => reader.readEntries(resolve, reject))
      if (!batch.length) break
      allEntries.push(...batch)
    }
    const nested = await Promise.all(
      allEntries.map((child) => readEntriesAsFiles(child, `${pathPrefix}${entry.name}/`)),
    )
    return nested.flat()
  }
  return []
}
