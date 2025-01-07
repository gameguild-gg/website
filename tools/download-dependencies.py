import os
import subprocess
import sys
import platform

# Repositories and their build instructions
repos = {
    "clang": {
        "url": "https://github.com/llvm/llvm-project.git",
        "branch": "main",
        "build_cmds": [
            "cmake -S llvm -B build -DCMAKE_BUILD_TYPE=Release",
            "cmake --build build --target clang",
        ],
    },
    "python": {
        "url": "https://github.com/python/cpython.git",
        "branch": "main",
        "build_cmds": [
            "./configure --enable-optimizations",
            "make -j$(nproc)",
        ],
    },
    "cheerp": {
        "url": "https://github.com/leaningtech/cheerp-compiler.git",
        "branch": "master",
        "build_cmds": [
            "cmake -S llvm -B ../build/llvm -C llvm/CheerpCmakeConf.cmake -DCMAKE_BUILD_TYPE=Release -DLLVM_ENABLE_PROJECTS=clang -DCLANG_ENABLE_OPAQUE_POINTERS=OFF -G Ninja",
            "cmake --build ../build/llvm",
        ],
    },
    "emscripten": {
        "url": "https://github.com/emscripten-core/emsdk.git",
        "branch": "main",
        "build_cmds": [
            "./emsdk install latest",
            "./emsdk activate latest",
            "source ./emsdk_env.sh",
        ],
    }
}


# Install dependencies
def install_dependencies():
    os_name = platform.system().lower()
    try:
        if "linux" in os_name:
            subprocess.run(["sudo", "apt", "update"], check=True)
            subprocess.run(["sudo", "apt", "install", "-y", "git", "cmake", "gcc", "g++", "make"], check=True)
        elif "darwin" in os_name:  # macOS
            subprocess.run(["brew", "install", "git", "cmake", "gcc"], check=True)
        elif "windows" in os_name:
            subprocess.run(["choco", "install", "-y", "git", "cmake", "visualstudio2022buildtools"], check=True)
        else:
            print("Unsupported OS")
            sys.exit(1)
    except subprocess.CalledProcessError:
        print("Error installing dependencies.")
        sys.exit(1)


# Clone and build repositories
def clone_and_build():
    for repo_name, repo_info in repos.items():
        print(f"Cloning and building {repo_name}...")
        try:
            # Clone repo
            if not os.path.exists(repo_name):
                subprocess.run(["git", "clone", "--branch", repo_info["branch"], repo_info["url"], repo_name],
                               check=True)
            os.chdir(repo_name)

            # Run build commands
            for cmd in repo_info["build_cmds"]:
                subprocess.run(cmd, shell=True, check=True)

            os.chdir("..")
        except subprocess.CalledProcessError:
            print(f"Error building {repo_name}.")
            os.chdir("..")
            continue


# Main script logic
def main():
    install_dependencies()
    clone_and_build()


if __name__ == "__main__":
    main()
