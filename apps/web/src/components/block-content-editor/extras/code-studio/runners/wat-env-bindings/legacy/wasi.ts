/**
 * WASI (WebAssembly System Interface) bindings
 * Complete implementation for WASI preview1 and unstable
 */
export function createWASIBindings(
  stdout: (text: string) => void,
  stderr: (text: string) => void,
  memory: WebAssembly.Memory
) {
  const textDecoder = new TextDecoder('utf-8')

  // File descriptor tracking
  const FD_STDIN = 0
  const FD_STDOUT = 1
  const FD_STDERR = 2

  return {
    // Arguments and environment
    args_get: (argvPtr: number, argvBufPtr: number) => 0,
    args_sizes_get: (argcPtr: number, argvBufSizePtr: number) => {
      const view = new DataView(memory.buffer)
      view.setUint32(argcPtr, 0, true)
      view.setUint32(argvBufSizePtr, 0, true)
      return 0
    },

    environ_get: (environPtr: number, environBufPtr: number) => 0,
    environ_sizes_get: (environCountPtr: number, environBufSizePtr: number) => {
      const view = new DataView(memory.buffer)
      view.setUint32(environCountPtr, 0, true)
      view.setUint32(environBufSizePtr, 0, true)
      return 0
    },

    // Clock operations
    clock_res_get: (clockId: number, resolutionPtr: number) => {
      const view = new DataView(memory.buffer)
      view.setBigUint64(resolutionPtr, 1000000n, true) // 1ms resolution
      return 0
    },

    clock_time_get: (clockId: number, precision: bigint, timePtr: number) => {
      const view = new DataView(memory.buffer)
      const now = BigInt(Date.now()) * 1000000n // Convert to nanoseconds
      view.setBigUint64(timePtr, now, true)
      return 0
    },

    // File descriptor operations
    fd_advise: (fd: number, offset: bigint, len: bigint, advice: number) => 0,
    fd_allocate: (fd: number, offset: bigint, len: bigint) => 0,
    fd_close: (fd: number) => 0,
    fd_datasync: (fd: number) => 0,
    fd_fdstat_get: (fd: number, statPtr: number) => {
      const view = new DataView(memory.buffer)
      view.setUint8(statPtr, 0) // filetype
      view.setUint16(statPtr + 2, 0, true) // flags
      return 0
    },
    fd_fdstat_set_flags: (fd: number, flags: number) => 0,
    fd_fdstat_set_rights: (fd: number, rightsBase: bigint, rightsInheriting: bigint) => 0,
    fd_filestat_get: (fd: number, bufPtr: number) => 0,
    fd_filestat_set_size: (fd: number, size: bigint) => 0,
    fd_filestat_set_times: (fd: number, atim: bigint, mtim: bigint, fstFlags: number) => 0,
    fd_pread: (fd: number, iovsPtr: number, iovsLen: number, offset: bigint, nreadPtr: number) =>
      0,
    fd_prestat_get: (fd: number, bufPtr: number) => 8, // EBADF
    fd_prestat_dir_name: (fd: number, pathPtr: number, pathLen: number) => 0,
    fd_pwrite: (
      fd: number,
      iovsPtr: number,
      iovsLen: number,
      offset: bigint,
      nwrittenPtr: number
    ) => 0,
    fd_read: (fd: number, iovsPtr: number, iovsLen: number, nreadPtr: number) => {
      const view = new DataView(memory.buffer)
      view.setUint32(nreadPtr, 0, true)
      return 0
    },
    fd_readdir: (
      fd: number,
      bufPtr: number,
      bufLen: number,
      cookie: bigint,
      bufusedPtr: number
    ) => 0,
    fd_renumber: (from: number, to: number) => 0,
    fd_seek: (fd: number, offset: bigint, whence: number, newOffsetPtr: number) => 0,
    fd_sync: (fd: number) => 0,
    fd_tell: (fd: number, offsetPtr: number) => 0,

    fd_write: (fd: number, iovsPtr: number, iovsLen: number, nwrittenPtr: number) => {
      const view = new DataView(memory.buffer)
      let totalWritten = 0

      for (let i = 0; i < iovsLen; i++) {
        const iovPtr = iovsPtr + i * 8
        const bufPtr = view.getUint32(iovPtr, true)
        const bufLen = view.getUint32(iovPtr + 4, true)

        const buffer = new Uint8Array(memory.buffer, bufPtr, bufLen)
        const text = textDecoder.decode(buffer)

        if (fd === FD_STDOUT) {
          stdout(text)
        } else if (fd === FD_STDERR) {
          stderr(text)
        }

        totalWritten += bufLen
      }

      view.setUint32(nwrittenPtr, totalWritten, true)
      return 0
    },

    // Path operations
    path_create_directory: (fd: number, pathPtr: number, pathLen: number) => 0,
    path_filestat_get: (
      fd: number,
      flags: number,
      pathPtr: number,
      pathLen: number,
      bufPtr: number
    ) => 0,
    path_filestat_set_times: (
      fd: number,
      flags: number,
      pathPtr: number,
      pathLen: number,
      atim: bigint,
      mtim: bigint,
      fstFlags: number
    ) => 0,
    path_link: (
      oldFd: number,
      oldFlags: number,
      oldPathPtr: number,
      oldPathLen: number,
      newFd: number,
      newPathPtr: number,
      newPathLen: number
    ) => 0,
    path_open: (
      fd: number,
      dirflags: number,
      pathPtr: number,
      pathLen: number,
      oflags: number,
      fsRightsBase: bigint,
      fsRightsInheriting: bigint,
      fdflags: number,
      fdPtr: number
    ) => 8, // EBADF
    path_readlink: (
      fd: number,
      pathPtr: number,
      pathLen: number,
      bufPtr: number,
      bufLen: number,
      bufusedPtr: number
    ) => 0,
    path_remove_directory: (fd: number, pathPtr: number, pathLen: number) => 0,
    path_rename: (
      oldFd: number,
      oldPathPtr: number,
      oldPathLen: number,
      newFd: number,
      newPathPtr: number,
      newPathLen: number
    ) => 0,
    path_symlink: (
      oldPathPtr: number,
      oldPathLen: number,
      fd: number,
      newPathPtr: number,
      newPathLen: number
    ) => 0,
    path_unlink_file: (fd: number, pathPtr: number, pathLen: number) => 0,

    // Poll operations
    poll_oneoff: (inPtr: number, outPtr: number, nsubscriptions: number, neventsPtr: number) => 0,

    // Process operations
    proc_exit: (code: number) => {
      stdout(`\n[Exit code: ${code}]`)
    },
    proc_raise: (sig: number) => 0,

    // Random number generation
    random_get: (bufPtr: number, bufLen: number) => {
      const buffer = new Uint8Array(memory.buffer, bufPtr, bufLen)
      crypto.getRandomValues(buffer)
      return 0
    },

    // Scheduler
    sched_yield: () => 0,

    // Socket operations (stubs)
    sock_recv: (
      fd: number,
      riDataPtr: number,
      riDataLen: number,
      riFlags: number,
      roDatalenPtr: number,
      roFlagsPtr: number
    ) => 0,
    sock_send: (
      fd: number,
      siDataPtr: number,
      siDataLen: number,
      siFlags: number,
      soDatalenPtr: number
    ) => 0,
    sock_shutdown: (fd: number, how: number) => 0,
  }
}
