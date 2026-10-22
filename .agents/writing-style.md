# Writing style

The main pattern is practical explanation with a conversational tone. Say what something does, show how you can use it, and explain any limitation that affects the result. Short pages can stay short!

This applies to READMEs, guides, comments meant for readers, pull request descriptions, and changelogs.

## Voice

Use plain English and exact technical names. A reader should be able to connect the explanation to a file, setting, command, input, or visible result.

- Start with what the project or page is for. One or two sentences are usually enough.
- Keep the project or feature as the subject when describing behavior. Use "you" when explaining a choice or a step.
- Explain why a detail matters. For example, describe what a setting changes instead of only saying that the setting exists.
- Use "I" only for actual personal context or uncertainty from the author. Do not invent an opinion or a promise on the author's behalf.
- A short aside, parentheses, or an occasional exclamation can fit. Keep most of the page focused on the subject.
- Avoid promotional wording and generic introductions. A concrete use case is more useful than calling a feature powerful or seamless.
- Use periods, commas, parentheses, or a spaced hyphen in prose. Avoid semicolons and em dashes. Keep punctuation inside code unchanged.

Existing pages may use stronger emphasis or informal wording. Use that when it helps explain the point, without repeating old typos or turning every paragraph into an aside.

## Page structure

Open with `# Page title` and a compact description. Add sections for the information the reader needs, such as `Usage`, `Inputs`, `How it works`, `Requirements`, or `Build`.

A small README may only need a description, a usage example, and a license. A larger guide can have examples, compatibility notes, and navigation. Scale the page to its contents.

- Use `##` for main sections and `###` for individual examples or topics.
- Use sentence case for headings, keeping project names and identifiers as written.
- Keep paragraphs short, with one explanation or decision per paragraph.
- Use bullets for features, options, or constraints. Use numbered steps when order matters.
- Use tables for support matrices, inputs and outputs, settings, or measured results.
- Add navigation links when the page is long enough to need them.
- Leave a blank line around headings, lists, tables, and code blocks.

Badges and logos belong on project READMEs when they provide useful links or status. Internal guides do not need them.

## Technical examples

Introduce an example with the task it performs. Put the code immediately after that explanation, then describe the result or any important failure case.

Use inline code for paths, commands, packages, types, functions, inputs, and settings. Use fenced blocks with a language tag, such as `yaml`, `sh`, `ps1`, `py`, or `cs`.

- Include setup that is needed to understand or run the example.
- Show only the options that matter for the example, and name them clearly.
- Show expected output in comments or a separate output block when it helps.
- State version, platform, or permission requirements close to the example.
- Label illustrative examples if they are not ready to run.

For example, a usage section for a reusable workflow could look like this:

```yaml
jobs:
  build:
    uses: owner/repo/.github/workflows/build.yml@main
    with:
      dotnet-version: 9.0.x
```

The job runs the shared build workflow with the given .NET SDK. Pin `@main` to a tag or commit if you need a stable version.

## Limitations and plans

State partial support, bugs, and workarounds directly. Explain what happens and what the reader can do about it. Put the note near the affected feature.

Use bold for a requirement or consequence that is easy to miss. A notice block such as `> [!NOTE]` or `> [!WARNING]` can hold a short compatibility note before an example. Use notice blocks sparingly for information that needs to stand apart. Ordinary facts do not all need emphasis.

Keep current behavior separate from plans. Only describe a future feature when there is an actual plan, and make its status clear. Do not invent support, versions, performance results, or release dates to make a page look complete.

## Links and credit

Link related projects, dependencies, detailed guides, and contributors where the reference is useful. Use relative links for pages in the same repository.

Keep exact domain terms and product names as the project uses them. Explain abbreviations if the intended reader may not know them.

## Changelogs and pull requests

Use compact bullets that name the change and the affected feature. Common opening verbs are `Added`, `Fixed`, `Updated`, `Renamed`, and `Removed`.

Add a short explanation when the change affects compatibility or requires an action from the reader. Group entries by package or component when a release touches several of them. Use version headings, dates, and release links when those details are known.

A pull request description follows the same idea. Say what changed and why in a few sentences, then list anything a reviewer or user needs to do.

An illustrative changelog entry:

- Added [Writing style](writing-style.md)
