import { spawn } from 'child_process';

class ExecuteCommandOptions {
  timeout?: number = 0;
  logoutput?: boolean = false;
  command: string;
  stdin?: string;
}

export class ExecuteCommandResult {
  stdout?: string;
  stderr?: string;
  duration?: number; // nanoseconds
}

async function ExecuteCommand(
  data: ExecuteCommandOptions,
): Promise<ExecuteCommandResult> {
  const { timeout, logoutput, command, stdin } = data;
  const startTime = process.hrtime();

  return new Promise<ExecuteCommandResult>((resolve, reject) => {
    const childProcess = spawn(command, {
      shell: true,
      stdio: ['pipe', 'pipe', 'pipe'],
    });

    let stdoutData = '';
    let stderrData = '';

    if (logoutput) {
      childProcess.stdout?.on('data', (data) => {
        process.stdout.write(data);
        stdoutData += data;
      });
      childProcess.stderr?.on('data', (data) => {
        process.stderr.write(data);
        stderrData += data;
      });
    } else {
      childProcess.stdout?.on('data', (data) => {
        stdoutData += data;
      });
      childProcess.stderr?.on('data', (data) => {
        stderrData += data;
      });
    }

    childProcess.on('error', (error) => {
      reject(error);
    });

    childProcess.on('close', (code) => {
      const endTime = process.hrtime(startTime);
      const durationInNano = endTime[0] * 1e9 + endTime[1];

      if (code === 0) {
        resolve({
          stdout: stdoutData,
          stderr: stderrData,
          duration: durationInNano,
        });
      } else {
        let x = stderrData.split('\n');
        reject(
          new Error(
            `Command failed with code ${code}:\n"${stderrData}"\noutput:\n"${stdoutData}"\nstdin:\n"${stdin}"\n`,
          ),
        );
      }
    });

    // Write to stdin
    childProcess.stdin?.write(stdin);
    childProcess.stdin?.end();

    // Timeout handling
    if (timeout > 0) {
      setTimeout(() => {
        childProcess.kill();
        reject(new Error('Command execution timed out'));
      }, timeout);
    }
  });
}

export default ExecuteCommand;
