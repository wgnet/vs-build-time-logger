# Maintainers

## Git Branching Policy

This project aims to keep a simple git history by having one long running branch (`main`) where branches are created, rebased onto, and then merged. Releases are simply tagged commits along the main branch.

When creating a branch, prefix the branch with the type of change being made:
- `feature/<feature-name>` for new features
- `bugfix/<bugfix-name>` for bux fixes

Prior to merging all branches should be rebased onto `main`, and ideally squashed into a single commit or a set of clear logical commits. After a merge, the branch should be deleted. 

When there is a commit suitable for releasing, it should be tagged with the release version. The version released should match the source code in this tagged commit.


## Handling a Pull Request

- Assess whether the pull request is inline with the intended purpose of the extension.
- Ensure pull requests have been rebased onto the `main` branch prior to accepting the merge request.

## Creating a Release

### Prior to Releasing

- Ensure all unit tests are passing. 
- As of this writing there are no end-to-end tests configured, so it is highly reccommended to run the extension in a test environment with access to logging endpoints (e.g. InfluxDB) and ensure data is being captured and logged as expected.

### Release Process

As of this writing there is no automated release process. As a result, the steps to create a release are as follows:

- Update the extension version in the `source.extension.vsixmanifest` file.
- Update `CHANGELOG.md` file with the changes made in this release version.
- Tag the commit 
- Upload the updated built extension to the Visual Studio marketplace.
