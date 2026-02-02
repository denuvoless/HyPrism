# Contributing Guide

We welcome contributions from the community! Whether it's fixing bugs, improving documentation, or adding new features, your help is appreciated.

## Development Workflow

1.  **Fork the Repository:** Click the "Fork" button on GitHub to create your own copy.
2.  **Clone:**
    ```bash
    git clone https://github.com/yyyumeniku/HyPrism.git
    cd HyPrism
    ```
3.  **Branching Strategy:**
    *   Always create a new branch for your work. Do not commit directly to `master`/`main`.
    *   **Naming Convention:** `type/short-description`
        *   `feat/ui-overhaul`
        *   `fix/crash-on-startup`
        *   `docs/update-readme`
4.  **Development:**
    *   Make your changes.
    *   Ensure the code compiles (`dotnet build`).
    *   Run any available tests.
5.  **Commit:**
    *   Write clear, concise commit messages.
    *   Reference Issue IDs if applicable (e.g., "Fix login error (#123)").
6.  **Push & Pull Request:**
    *   Push to your fork.
    *   Open a Pull Request (PR) to the official repository.
    *   Fill out the PR template describing what you changed.

## Requirements

*   **IDE:** Visual Studio 2022 (Windows) or Rider (Multiplatform) / VS Code.
*   **SDK:** .NET 10.0 (Preview).
*   **Git:** Basic knowledge of git operations.

## Reporting Issues

If you find a bug but can't fix it:
1.  Go to the **Issues** tab.
2.  Search if the issue already exists.
3.  Create a new Issue providing:
    *   Logs (`run.log`).
    *   Screenshots (if visual).
    *   Steps to reproduce.
