# Documentation style

Use ASD-STE100 writing principles for English documentation created in this fork.
The specification contains writing rules and a controlled dictionary.
This project style guide does not certify formal compliance.
Refer to the [official ASD-STE100 description](https://www.asd-ste100.org/about.html).

## Write clear technical instructions

- Use one meaning for each technical term. Use the terms in the table below.
- Use active voice and name the actor.
- Put one action in each numbered step.
- Give the condition before the action when the condition matters.
- Keep instructions short. Aim for at most 20 words per procedural sentence and 25 words per descriptive sentence.
- Use exact names for UI controls, files, commands, fields and error codes.
- Keep code, identifiers, paths, flags and quoted UI text unchanged.
- Use a table for parallel choices and a numbered list for an ordered procedure.
- Separate prerequisites, actions, expected results, limitations and recovery.
- Give evidence before you state that a defect is fixed or a gate is complete.

## Terms

| Term | Meaning |
|---|---|
| GUI | The usual-user WinUI application. |
| tray | The elevated process that starts components and performs privileged operations. |
| component | A managed traffic utility installed or selected by the user. |
| Store | The application catalog and installation flow. |
| preset | Component options and related files. |
| gate | A milestone with explicit exit criteria. |
| candidate | An exact revision and artifact set proposed for release. |
| native check | A check using the affected Windows or UI implementation. State fixture and installed-product limits. |
| verified | The stated procedure ran against the stated revision and produced recorded evidence. |

## Preserve upstream authorship

The main README keeps its original structure, voice and attribution.
Documents introduced by Storik4pro receive targeted factual, spelling, link and navigation improvements.
Do not rewrite those documents into a different style merely because the fork modified them later.
Use the first introduction and upstream history to classify a file; the latest author is insufficient.

Legal texts, license notices and third-party attribution must retain their legal meaning.
Do not apply a mechanical vocabulary rewrite to them.
The [authorship inventory](governance/document-inventory.json) records the classification and editing policy.

## Maintain English and Russian versions

Keep facts, prerequisites, commands and limitations consistent across languages.
Link each existing language counterpart at the start of the document.
Use Russian technical prose in Russian documents; ASD-STE100 controls English, not Russian grammar.
Preserve stable anchors where other documents depend on them.
The [documentation index](README.md) is the common entry point.

## Review a document

1. Check technical statements against the current code and test evidence.
2. Check the file's authorship policy before changing its style.
3. Check instructions, relative links, tables and code fences.
4. Check the counterpart language when behavior changes.
5. Record untested commands and historical results explicitly.

Link checking is automated. Technical accuracy and controlled vocabulary still require review.
