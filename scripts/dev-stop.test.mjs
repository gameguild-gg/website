import assert from "node:assert/strict";
import test from "node:test";
import {
  isRepositoryApiWatcherCommand,
  parsePosixProcessList,
} from "./dev-stop.mjs";

test("recognizes the repository API dotnet watcher", () => {
  assert.equal(
    isRepositoryApiWatcherCommand(
      "dotnet watch --project apps/api/Source/Modules/GameGuild.Other/Other.csproj run",
    ),
    false,
  );
  assert.equal(
    isRepositoryApiWatcherCommand(
      "dotnet watch --project apps/api/Source/GameGuild.API/GameGuild.API.csproj run --urls http://localhost:8080",
    ),
    true,
  );
  assert.equal(
    isRepositoryApiWatcherCommand(
      "/usr/bin/dotnet watch --project /workspace/apps/api/Source/GameGuild.API/GameGuild.API.csproj run",
    ),
    true,
  );
});

test("recognizes the dotnet watch tool child without selecting other workers", () => {
  assert.equal(
    isRepositoryApiWatcherCommand(
      "/usr/share/dotnet/dotnet /usr/share/dotnet/sdk/10.0.401/DotnetTools/dotnet-watch/10.0.401/tools/net10.0/any/dotnet-watch.dll --project apps/api/Source/GameGuild.API/GameGuild.API.csproj run",
    ),
    true,
  );
  assert.equal(
    isRepositoryApiWatcherCommand(
      "/usr/share/dotnet/dotnet /usr/share/dotnet/sdk/10.0.401/MSBuild.dll /nologo /nodemode:1",
    ),
    false,
  );
});

test("parses POSIX process groups without truncating commands", () => {
  assert.deepEqual(
    parsePosixProcessList(
      " 837568 1689 837568 dotnet watch --project apps/api/Source/GameGuild.API/GameGuild.API.csproj run\n",
    ),
    [
      {
        pid: 837568,
        parentPid: 1689,
        processGroupId: 837568,
        command:
          "dotnet watch --project apps/api/Source/GameGuild.API/GameGuild.API.csproj run",
      },
    ],
  );
});
