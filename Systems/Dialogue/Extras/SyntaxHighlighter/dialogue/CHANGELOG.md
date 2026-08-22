# Change Log

All notable changes to the "dialogue" extension will be documented in this file.

Check [Keep a Changelog](http://keepachangelog.com/) for recommendations on how to structure this file.

## [1.4.1]

- Added highlighting for text expressions: `${survivorCount}` inside narration, speaker lines, option
  text and attribute values. The whole tag is green (in the stock VS Code themes as well as in the
  Dialogue Theme), with the `${` `}` in bold.

## [1.3.3]

- Branch weights no longer need the `%`: `{25}=>Key` highlights as a weight, same as `{25%}=>Key`.

## [1.3.2]

- Added highlighting for code attached to an option: `*(Exit)=>{ ... }->Key`.
- The arrow in an option line is now coloured as a redirect, like every other `->`/`=>`, instead of
  being part of the option text.

## [1.3.1]

- Code blocks are now highlighted with the C# grammar itself (the grammar delegates to `source.cs`
  inside `{ ... }`), so expressions get real keyword/string/number/call colouring instead of one flat
  colour. Falls back to the block colour for anything C# does not scope.

## [1.3.0]

- Added highlighting for element attributes: `@name=value`, and bare `@name` flags.
- Added highlighting for random branch weights, so `{25%}=>Key` reads as a weight instead of as a code block.
- Narration no longer swallows lines starting with `@`.

## [Unreleased]

- Initial release