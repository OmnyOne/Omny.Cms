# Agent Notes

## Summary of Recent Changes
- Introduced `Omny.Cms.Abstractions` project containing shared plugin interfaces.
- Unified editor and field plugin registries into a single `IPluginRegistry` service.
- Removed the obsolete `MarkdownOrHtml` field type from tests and manifest logic.
- Added a project-wide `README.md` with the default manifest JSON.
- Updated image selector UI and field plugin system.
- Expanded unit tests for new manifest structure.
- Consolidated `IEditorPlugin` into `IFieldPlugin` so all plugins share a common
  interface.
- Added UI components `CompoundFieldEditor` and `CollectionFieldEditor` to allow
  editing of compound fields and collections.
- Default manifest now includes a `Path` text field for `Page` content types.
- Plugins now expose `DefaultValue` so the editor can initialize missing fields.
- Field plugins now provide `DisplayName` and optional `Icon` for use in the UI.
- Collection editor shows a button per allowed field type and initializes new
  entries with each plugin's default value.
- Introduced `ImageTextFieldPlugin` that wraps the `CompoundFieldEditor` so
  compound fields behave like standard plugins.
- Compound field definitions now specify `SubFields` with per-field labels.
- Collection editor no longer allows switching item types via dropdown.
- Fixed missing dictionary keys when adding new collection entries.
- Image selector uses a full-screen modal with flexbox grid so images are easier
  to browse and select.
- MudBlazor added for UI components and the image selector now opens as a full-screen MudDialog.

## Architecture Decisions
- Shared abstractions allow external packages to implement custom plugins.
- Compound fields are persisted as JSON files. The `ImageText` type combines image and text values.
- Collections continue to reference entry files via a list file for simplicity.
- Plugins render using Blazor `RenderFragment` for flexibility.
- Compound and collection field editors are implemented as Blazor components
  that leverage the existing plugin system for nested fields.
- MudBlazor is included for dialogs and other Material design elements.
- Field definitions now store a stable `Name` used as the identifier and a
  separate `Label` for display purposes.
- Compound field definitions include `SubFields` describing internal field order
  and optional labels for each subfield.

## Notes for Future Agents
- Nested collections are not fully supported yet; current implementation handles only a single level.
- The `ImageText` compound field data is stored as raw JSON without schema validation.
- UI components are minimal; consider improving error handling and customization.
- Collection editor supports adding and removing items but lacks drag-and-drop ordering.
- Plugins currently use simple emoji icons; consider replacing with a more
  robust icon system.
- Compound fields now allow per-subfield labels (use null for no label).
- Items in a collection use the field type selected at creation and cannot be changed later.


- Group related minimal APIs in extension methods and register them with `apiGroup.AddSomething()`.
- Always use braces for `if`, `else`, loops, and method bodies. Assign intermediate variables before returning values.
