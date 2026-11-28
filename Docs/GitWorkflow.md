# Git workflow - GrassRendering

Work happens on **one branch per phase** of the [implementation plan](Implementation/README.md). A phase reaches `main` through a pull request after its stop and review, and a **tag** marks where it ends. So `main` always holds finished, tested phases.

| Convention | Value |
|---|---|
| Branch | `phase-<number>-<short name>`: `phase-1-spikes`, `phase-2-core` |
| Sub-phase (1b) | Stays on its phase's branch: Phase 1b continues on `phase-1-spikes` |
| Tag | `phase-<number>` on the merge commit in `main`: `phase-0`, `phase-1` |
| Commit | Small steps (one item, one fix), message `Phase 2 - item 3: arena and block pools` |

## Coming from SVN

| SVN | Git |
|---|---|
| `svn commit` sends straight to the server | `git commit` stays on your PC; `git push` sends it to GitHub |
| A branch is a copy of a folder on the server (`svn copy`) | A branch is a label on a commit: free and instant |
| Switching branch = another checkout folder | `git switch` changes the files in place, in the same folder |
| `svn update` | `git pull` |
| TortoiseSVN "Diff with base" | VS Code Source Control view (Ctrl+Shift+G), click a file |

## Start a phase

1. Check that nothing is left uncommitted: `git status`. Commit and push on `main` what is not phase work (a fix, a doc) before branching:
    ```
    git switch main
    git pull
    git add <files>
    git commit -m "Short message"
    git push
    ```
2. Create the branch and switch to it (`-c` = create; the files do not change):
    ```
    git switch -c phase-2-core
    ```
3. Push it once, as a backup (`-u` links it: a plain `git push` is enough afterwards):
    ```
    git push -u origin phase-2-core
    ```
4. Check: `git branch -vv` shows `* phase-2-core ... [origin/phase-2-core]`.

## During the phase

```
git status          # what changed; new files show as "untracked"
git diff            # the content of the changes
git add -A          # stage everything, new .meta files included
git commit -m "Phase 2 - item 3: arena and block pools"
git push
```

- **Unity:** always commit a `.meta` file with its asset; `git add -A` takes both.
- **Commit before switching branch:** otherwise git carries the uncommitted changes to the other branch, or refuses to switch.
- **Switching with the Editor open** is fine: Unity reimports the changed files (`Library/` is not versioned).
- **Unity reserialization:** Unity sometimes rewrites settings assets it saves (format upgrade, list order). Commit it with a clear message ("Unity 6000.6 reserialization") so nobody looks for a hidden change.
- **Never commit:** LibreOffice lock files `.~lock.*#` (a CSV still open); `Logs/` and `Builds/` are ignored.
- **Git LFS** must be installed before cloning (large binary assets, rules in `.gitattributes`).

## End of the phase: merge into main

After the phase's stop and review, merge through a pull request: it shows the whole phase as one diff.

**Pull request on GitHub (recommended)**

1. Everything committed and pushed: `git status` clean, then `git push`.
2. Open the pull request, from the command line or on github.com ("Compare & pull request"):
    ```
    gh pr create --base main --head phase-2-core --title "Phase 2 - Core pipeline"
    ```
3. Review the "Files changed" tab. For `.md` files, the document icon "Display the rich diff" shows the rendered Markdown with additions in green and removals in red.
4. Click **Merge pull request**.

**Local merge (without GitHub)**

```
git switch main
git merge --no-ff phase-2-core    # --no-ff keeps a merge commit: the phase stays visible in the history
git push
```

## After the merge: tag, then clean up

1. Tag the merge commit on an up-to-date `main` and push the tag (`git push` alone does not push tags):
    ```
    git switch main
    git pull
    git tag -a phase-2 -m "Phase 2 - Core pipeline done"
    git push origin phase-2
    ```
2. Delete the finished branch (only the label: its commits stay in `main`):
    ```
    git branch -d phase-2-core
    git push origin --delete phase-2-core
    ```
    GitHub can also delete it from the pull request page ("Delete branch").
3. Start the next phase from `main` (Start a phase, above).

A past phase can be tagged afterwards on its commit, for example Phase 0 (commit `bbb4a2c`):
```
git tag -a phase-0 bbb4a2c -m "Phase 0 - Project setup done"
git push origin phase-0
```

## Using the tags

The start of a phase is the tag of the previous one.

| To | Command |
|---|---|
| List the tags | `git tag` |
| See the tags in the history | `git log --oneline --graph --decorate --branches --remotes --tags` (graphical: see Seeing the history) |
| Everything a phase changed | `git diff phase-1 phase-2` |
| The commits of a phase | `git log --oneline phase-1..phase-2` |
| The project as it was at the end of a phase (read only) | `git switch --detach phase-1`, then `git switch main` to come back |

On GitHub, the tags are under Code > Tags; each one can be downloaded as a zip of that state.

## Special cases

| Situation | Command |
|---|---|
| Typo in a branch name, not pushed yet (`src refspec ... does not match any` on push) | `git branch -m phase-2-core` renames the current branch |
| Throw a branch away (a failed spike): `main` was never touched | `git switch main`, then `git branch -D phase-2-core` (capital D forces it) |
| See all branches and where you are | `git log --oneline --graph --decorate --branches --remotes --tags` |
| Diff of the whole phase | `git diff main...phase-2-core` |
| Whole file shown as changed (line endings) | `git diff --ignore-cr-at-eol`; in VS Code, "Ignore Trim Whitespace" in the diff editor's `...` menu |

## Seeing the history

The history is a straight line until a phase branch is merged into `main`; after that, the graph shows one lane per branch.

- **VS Code:** Source Control view (Ctrl+Shift+G), **Graph** section: lanes, branches and tags; click a commit to see its files.
- **gitk** (included with Git for Windows): `gitk --branches --remotes --tags &` in Git Bash.
- **GitHub:** Insights > Network.
- **Terminal:** `git log --oneline --graph --decorate --branches --remotes --tags`. Avoid `--all`: it also lists local references other tools create, for example AI agent checkpoints under `refs/agents/...` ("Agent host session ... - turn N" commits). They are never pushed and not part of any branch.

## Reviewing diffs in VS Code

- Source Control view (Ctrl+Shift+G): click a file under Changes for a side-by-side diff.
- Next / previous change: Alt+F5 / Shift+Alt+F5. Compact list of the changes: F7.
- Long lines (the docs): Alt+Z for word wrap; "Collapse unchanged regions" in the diff editor's `...` menu.
- The Markdown preview cannot show a diff: diff the source, open the preview beside it (Ctrl+K V), or use GitHub's rich diff on the pull request.
- A whole branch in VS Code, once configured:
    ```
    git config --global diff.tool vscode
    git config --global difftool.vscode.cmd 'code --wait --diff "$LOCAL" "$REMOTE"'
    git difftool main...phase-2-core
    ```
