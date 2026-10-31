/**
 * Git operations module
 *
 * Wraps simple-git to provide the operations the agent pipeline needs.
 * All destructive operations (push, branch delete) require explicit
 * confirmation from the developer before execution.
 */

import { simpleGit, type SimpleGit } from "simple-git";
import fs from "fs";
import path from "path";
import type { CommitSummary, FileDiff } from "../types/protocol.js";

export class GitClient {
  private git: SimpleGit;
  private repoPath: string;

  constructor(repoPath: string) {
    this.repoPath = repoPath;
    this.git = simpleGit(repoPath);
  }

  async getProjectName(): Promise<string> {
    return path.basename(this.repoPath);
  }

  async getCurrentBranch(): Promise<string> {
    const status = await this.git.status();
    return status.current ?? "unknown";
  }

  async getRecentCommits(count = 5): Promise<CommitSummary[]> {
    const log = await this.git.log({ maxCount: count });
    return log.all.map((entry) => ({
      hash: entry.hash,
      message: entry.message,
      author: entry.author_name,
      date: entry.date,
    }));
  }

  /**
   * Returns a flat list of file paths relative to the repo root,
   * excluding common non-source directories.
   */
  async getFileTree(maxDepth = 4): Promise<string[]> {
    const IGNORE = new Set([
      "node_modules",
      ".git",
      "dist",
      "build",
      ".next",
      "coverage",
      "__pycache__",
      ".venv",
      "venv",
      "target", // Rust/Cargo
      ".DS_Store",
    ]);

    const results: string[] = [];
    const repoPathCapture = this.repoPath;

    function walk(dir: string, depth: number): void {
      if (depth > maxDepth) return;
      const entries = fs.readdirSync(dir, { withFileTypes: true });
      for (const entry of entries) {
        if (IGNORE.has(entry.name)) continue;
        const full = path.join(dir, entry.name);
        if (entry.isDirectory()) {
          walk(full, depth + 1);
        } else {
          results.push(path.relative(repoPathCapture, full));
        }
      }
    }
    walk(this.repoPath, 0);
    return results;
  }

  /**
   * Read a file from the repo. Returns null if not found.
   * Refuses to read files outside the repo root.
   */
  readFile(relativePath: string): string | null {
    const abs = path.resolve(this.repoPath, relativePath);
    if (!abs.startsWith(this.repoPath)) return null; // path traversal guard
    if (!fs.existsSync(abs)) return null;
    return fs.readFileSync(abs, "utf-8");
  }

  /**
   * Write a file to the repo. Creates parent directories if needed.
   * Refuses to write outside the repo root.
   */
  writeFile(relativePath: string, content: string): void {
    const abs = path.resolve(this.repoPath, relativePath);
    if (!abs.startsWith(this.repoPath)) {
      throw new Error(`Path traversal detected: ${relativePath}`);
    }
    fs.mkdirSync(path.dirname(abs), { recursive: true });
    fs.writeFileSync(abs, content, "utf-8");
  }

  /**
   * Stage all modified/added files and return a diff of staged changes.
   */
  async stageAll(): Promise<FileDiff[]> {
    await this.git.add(".");
    return this.getStagedDiff();
  }

  /**
   * Get the diff of staged changes.
   */
  async getStagedDiff(): Promise<FileDiff[]> {
    const diff = await this.git.diff(["--cached", "--stat"]);
    const patch = await this.git.diff(["--cached"]);
    return parseDiff(patch, diff);
  }

  /**
   * Get the diff of unstaged changes.
   */
  async getUnstagedDiff(): Promise<FileDiff[]> {
    const patch = await this.git.diff();
    return parseDiff(patch, "");
  }

  /**
   * Commit staged changes.
   */
  async commit(message: string): Promise<string> {
    const result = await this.git.commit(message);
    return result.commit;
  }

  /**
   * Push the current branch to origin.
   * This is a destructive network operation — only call after developer approval.
   */
  async push(): Promise<void> {
    const branch = await this.getCurrentBranch();
    await this.git.push("origin", branch, ["--set-upstream"]);
  }

  /**
   * Undo the last commit (keeps changes staged).
   */
  async undoLastCommit(): Promise<void> {
    await this.git.reset(["--soft", "HEAD~1"]);
  }

  /**
   * Unstage all staged changes (does not discard them).
   */
  async unstageAll(): Promise<void> {
    await this.git.reset(["HEAD"]);
  }

  /**
   * Discard all uncommitted changes. DESTRUCTIVE.
   * Only call after explicit developer approval.
   */
  async discardChanges(): Promise<void> {
    await this.git.checkout(["."]);
    await this.git.clean("f", ["-d"]);
  }

  async switchBranch(branch: string): Promise<void> {
    await this.git.checkout(branch);
  }

  async createBranch(branch: string): Promise<void> {
    await this.git.checkoutLocalBranch(branch);
  }
}

/**
 * Parse a unified diff string into FileDiff objects.
 * This is a minimal parser for the prototype — enough to display in VR panels.
 */
function parseDiff(patch: string, _stat: string): FileDiff[] {
  const diffs: FileDiff[] = [];
  if (!patch.trim()) return diffs;

  // Split by file header
  const fileSections = patch.split(/^diff --git /m).filter(Boolean);

  for (const section of fileSections) {
    const lines = section.split("\n");
    const header = lines[0] ?? "";

    // Extract file path from "a/path b/path"
    const pathMatch = header.match(/a\/(.+) b\/(.+)/);
    if (!pathMatch) continue;
    const filePath = pathMatch[2] ?? pathMatch[1] ?? "unknown";

    let status: FileDiff["status"] = "modified";
    if (section.includes("\nnew file mode")) status = "added";
    if (section.includes("\ndeleted file mode")) status = "deleted";

    const additions = (section.match(/^\+[^+]/gm) ?? []).length;
    const deletions = (section.match(/^-[^-]/gm) ?? []).length;

    diffs.push({
      path: filePath.replace(/^b\//, ""),
      status,
      patch: section,
      additions,
      deletions,
    });
  }

  return diffs;
}
